using AispaStudio.Desktop.ViewModels;

namespace AispaStudio.Desktop.Design;

/// <summary>
/// DataContext für die Vorschau des Designers (<c>Design.DataContext</c> mit <c>x:Static</c>). Der XAML-Compiler entfernt
/// <c>Design.*</c> aus der App; diese Eigenschaften liest nur die Vorschau. Öffentlich, weil die Vorschau sie zur Laufzeit auflöst.
/// </summary>
public static class DesignData
{
    /// <summary>Kundenliste mit Beispielkunden; die erste Kundenkarte ist geöffnet und lädt ihre Termine über <see cref="DesignApi"/>.</summary>
    public static CustomersViewModel Customers
    {
        get
        {
            var first = DesignCustomers.All[0];
            var customers = new CustomersViewModel(DesignCustomers.Api(first, TimeProvider.System.GetLocalNow()).Client(), TimeProvider.System);
            foreach (var customer in DesignCustomers.All)
            {
                customers.Results.Add(customer);
            }

            customers.Selected = first;
            return customers;
        }
    }
}
