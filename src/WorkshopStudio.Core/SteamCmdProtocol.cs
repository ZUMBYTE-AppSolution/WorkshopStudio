using System.Text;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public enum SteamLoginSignal { Password, GuardCode, MobileApproval, Authenticated }

/// <summary>Streaming decoder for ConPTY control sequences, including split CSI/OSC sequences.</summary>
public sealed class TerminalTextFilter
{
    private enum State { Text, Escape, Csi, Osc, OscEscape, Charset }
    private State _state;
    public string Append(string chunk)
    {
        var text = new StringBuilder(chunk.Length);
        foreach (var c in chunk)
        {
            switch (_state)
            {
                case State.Text:
                    if (c == '\u001b') _state = State.Escape;
                    else if (c == '\u009b') _state = State.Csi;
                    else if (c == '\u009d') _state = State.Osc;
                    else if (c == '\r' || c == '\n' || c == '\t' || !char.IsControl(c)) text.Append(c);
                    break;
                case State.Escape:
                    _state = c switch { '[' => State.Csi, ']' => State.Osc, '(' or ')' => State.Charset, _ => State.Text };
                    break;
                case State.Csi:
                    if (c >= '@' && c <= '~') _state = State.Text;
                    break;
                case State.Osc:
                    if (c == '\a') _state = State.Text;
                    else if (c == '\u001b') _state = State.OscEscape;
                    break;
                case State.OscEscape:
                    _state = c == '\\' ? State.Text : State.Osc;
                    break;
                case State.Charset: _state = State.Text; break;
            }
        }
        return text.ToString();
    }
}

/// <summary>Prompts are not required to end in a newline. No time-based prompt suppression.</summary>
public sealed class SteamCmdPromptDetector
{
    private readonly StringBuilder _tail = new();
    private static readonly Regex Password = new(
        @"(?:^|[\r\n])\s*(?:(?:please\s+)?enter\s+(?:your\s+)?)?password(?:\s+for[^\r\n:]*)?\s*[:>]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Guard = new(
        @"(?:^|[\r\n])\s*(?:(?:please\s+)?enter\s+(?:the\s+|your\s+)?)?(?:(?:steam\s*guard|two[- ]?factor|auth(?:entication|enticator)?|e-?mail|security|confirmation|verification)\s+)?code(?:\s+(?:from|for|sent|generated)[^\r\n:>]*|\s+\([^)]+\))?\s*[:>]\s*$|(?:please\s+)?enter[^\r\n]*(?:guard|authenticator|two[- ]factor)[^\r\n]*[:>]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Mobile = new(
        @"(?:confirm|approve|waiting)[^\r\n]*(?:mobile\s+app|steam\s+mobile|steam\s+app)|(?:mobile\s+app|steam\s+mobile)[^\r\n]*(?:confirm|approve|notification)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Ready = new(
        @"(?:Waiting\s+for\s+user\s+info|Logged\s+in|Logging\s+in[^\r\n]*)\s*(?:\.{1,3}\s*)?(?:\r?\n\s*)?OK\b|Waiting\s+for\s+user\s+info[^\r\n]*\bOK\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SteamLoginSignal? Append(string text)
    {
        _tail.Append(text);
        if (_tail.Length > 8192) _tail.Remove(0, _tail.Length - 8192);
        var current = _tail.ToString();
        SteamLoginSignal? signal = null;
        // A real code prompt takes priority over the explanatory mobile-authentication paragraph.
        if (Guard.IsMatch(current)) signal = SteamLoginSignal.GuardCode;
        else if (Password.IsMatch(current)) signal = SteamLoginSignal.Password;
        else if (Ready.IsMatch(current)) signal = SteamLoginSignal.Authenticated;
        else if (Mobile.IsMatch(current)) signal = SteamLoginSignal.MobileApproval;
        if (signal is not null) _tail.Clear();
        return signal;
    }

    public static bool IsAuthenticated(string text)
    {
        var ready = Ready.Matches(text).Cast<Match>().LastOrDefault();
        if (ready is null) return false;
        var failed = Regex.Matches(text, @"(?:FAILED|ERROR)[^\r\n]*(?:login|log[ -]?on)|Login\s+(?:Failure|failed)", RegexOptions.IgnoreCase)
            .Cast<Match>().LastOrDefault();
        return failed is null || ready.Index > failed.Index;
    }
    public static ulong? ReadSteamId64(string text)
    {
        var match=Regex.Match(text,@"(?i)\bSteamID:\s*\[U:1:(?<account>[0-9]{1,12})\]");
        if(!match.Success||!ulong.TryParse(match.Groups["account"].Value,out var account))return null;
        return 76561197960265728UL+account;
    }

    public static string DescribeFailure(string text, int exitCode)
    {
        if (Regex.IsMatch(text, @"InvalidPassword|invalid\s+password|incorrect\s+(?:password|login)|password\s+(?:is\s+)?incorrect", RegexOptions.IgnoreCase))
            return "Steam hat Loginname oder Passwort abgelehnt. Den Steam-Kontonamen verwenden, nicht Profilname oder E-Mail. Ein veraltetes gespeichertes Passwort unter 'Gespeichertes Passwort löschen' entfernen.";
        if (Regex.IsMatch(text, @"RateLimit|too\s+many|rate\s+limit|throttled", RegexOptions.IgnoreCase))
            return "Steam begrenzt weitere Anmeldeversuche. Nicht wiederholt anmelden; später erneut versuchen.";
        if (Regex.IsMatch(text, @"AccountLogonDenied|TwoFactor|InvalidLoginAuthCode|expired\s+code|auth(?:entication)?\s+code.*(?:invalid|incorrect)", RegexOptions.IgnoreCase))
            return "Steam Guard hat die Anmeldung nicht bestätigt. Aktuellen Code oder mobile Freigabe prüfen. Die originale SteamCMD-Konsole steht als Alternative bereit.";
        if (Regex.IsMatch(text, @"needs\s+to\s+be\s+online|NoConnection|ServiceUnavailable|connection.*(?:failed|timeout)|connect.*(?:failed|timed out)", RegexOptions.IgnoreCase))
            return "SteamCMD konnte Steam nicht erreichen. Internetzugang, Proxy und Firewallfreigabe für steamcmd.exe prüfen. Sicherheitssoftware nicht pauschal abschalten.";
        if (Regex.IsMatch(text, @"Access\s+is\s+denied|Permission\s+denied|failed\s+to\s+(?:write|save)", RegexOptions.IgnoreCase))
            return "SteamCMD kann seine Installation oder Sitzung nicht schreiben. Einen beschreibbaren Ordner im eigenen Benutzerprofil verwenden.";
        return $"SteamCMD hat die Anmeldung nicht bestätigt (Exitcode {exitCode}). Das bereinigte Login-Protokoll unten zeigt die Steam-Meldungen. Bei nicht erkannten Abfragen die originale SteamCMD-Konsole verwenden.";
    }
}

public static class WindowsCommandLine
{
    // CommandLineToArgvW/CRT quoting, not cmd.exe shell escaping. Secrets never enter this string.
    public static string Quote(string argument)
    {
        if (argument.IndexOfAny(['\0', '\r', '\n']) >= 0) throw new ArgumentException("Ungültiges Prozessargument.");
        var value = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') { value.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
            value.Append('\\', slashes).Append(c); slashes = 0;
        }
        value.Append('\\', slashes * 2).Append('"');
        return value.ToString();
    }
}
