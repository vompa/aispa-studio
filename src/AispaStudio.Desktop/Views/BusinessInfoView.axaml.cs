using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Views;

public partial class BusinessInfoView : FocusRequestView
{
    public BusinessInfoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is BusinessInfoViewModel model)
            {
                model.ContactActions ??= new CalendarView.WindowContactActions(this);
            }
        };
    }
}
