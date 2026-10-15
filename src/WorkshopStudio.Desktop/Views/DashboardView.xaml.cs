using System.Windows;
using System.Windows.Controls;
namespace WorkshopStudio.Desktop.Views;
public partial class DashboardView : UserControl
{
    public static readonly DependencyProperty GameColumnsProperty=DependencyProperty.Register(nameof(GameColumns),typeof(int),typeof(DashboardView),new PropertyMetadata(1));
    public int GameColumns { get=>(int)GetValue(GameColumnsProperty); private set=>SetValue(GameColumnsProperty,value); }
    public DashboardView() { InitializeComponent(); }
    private void Dashboard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if(KpiGrid is null || LeftColumn is null)return;
        var stacked=e.NewSize.Width<1030;
        var gameArea=stacked?e.NewSize.Width-55:e.NewSize.Width*1.7/2.7-75;
        GameColumns=gameArea>=690?2:1;
        KpiGrid.Columns=e.NewSize.Width<910?2:4;
        LeftColumn.Width=new GridLength(stacked?1:1.7,GridUnitType.Star);
        RightColumn.Width=stacked?new GridLength(0):new GridLength(1,GridUnitType.Star);
        Grid.SetColumn(ActivityPanel,stacked?0:1);Grid.SetRow(ActivityPanel,stacked?1:0);
        LeftContent.Margin=new Thickness(0,0,stacked?0:18,0);
        QuickActions.Columns=e.NewSize.Width<650?2:3;
    }
}
