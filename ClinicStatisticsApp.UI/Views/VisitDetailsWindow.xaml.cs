using System.Windows;
namespace ClinicStatisticsApp.UI.Views;
public partial class VisitDetailsWindow : Window
{
 public VisitDetailsWindow(PatientDossierWindow.VisitRow visit, IEnumerable<PatientDossierWindow.ServiceRow> services, IEnumerable<PatientDossierWindow.PaymentRow> payments)
 { InitializeComponent(); HeaderText.Text=$"{visit.Date} · {visit.Branch}\nСумма: {visit.Amount}\n{visit.Comment}"; ServicesList.ItemsSource=services.Select(x=>$"{x.Name} · {x.Quantity} · {x.Amount}"); PaymentsList.ItemsSource=payments.Select(x=>$"{x.Amount} · {x.Description} · касса {x.Cashbox}"); }
}
