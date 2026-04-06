using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClinicStatisticsApp.Services
{
    public class SummaryBookPdfExportService
    {
        private readonly SummaryGeneralService _summaryGeneralService = new SummaryGeneralService();
        private readonly SummaryProfoService _summaryProfoService = new SummaryProfoService();
        private readonly SummaryAdminService _summaryAdminService = new SummaryAdminService();

        public void ExportMonthlySummaryBook(string filePath, int year, int month)
        {
            var general = _summaryGeneralService.Build(year, month);
            var profo = _summaryProfoService.Build(year, month);
            var admin = _summaryAdminService.Build(year, month);

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Content().Column(column =>
                    {
                        column.Item().Text($"Статистика общая за {GetMonthName(month)} {year}")
                            .FontSize(18).Bold().AlignCenter();

                        column.Item().PaddingTop(10).Text("ФИЛИАЛЫ").Bold().FontSize(12);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(35);
                                columns.RelativeColumn(2.5f);
                                for (int i = 0; i < 13; i++)
                                    columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("№").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Сотрудник").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Явка").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Неявка").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Всего").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("ЦК").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Комфорт").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Баграмяна").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Детство").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Генделя").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Виктория").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Альфа").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Регион").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Артиллерийская").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Сельма").Bold();
                            });

                            foreach (var item in general.BranchRows)
                            {
                                table.Cell().Border(1).Padding(4).Text(item.Number.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.EmployeeFullName);
                                table.Cell().Border(1).Padding(4).Text(item.AttendanceTotal.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.AbsenceTotal.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.GrandTotal.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Ck.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Comfort.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Bagramyana.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Detstvo.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Gendelya.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Viktoriya.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Alfa.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Region.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Artilleriyskaya.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Selma.ToString());
                            }

                            table.Cell().Border(1).Padding(4).Text("Итого по филиалам").Bold();
                            table.Cell().Border(1).Padding(4).Text("").Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.AttendanceTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.AbsenceTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.GrandTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Ck.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Comfort.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Bagramyana.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Detstvo.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Gendelya.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Viktoriya.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Alfa.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Region.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Artilleriyskaya.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(general.BranchTotals.Selma.ToString()).Bold();
                        });

                        column.Item().PaddingTop(10)
                            .Text($"Всего по системе клиник: Явка={general.SystemTotals.AttendanceTotal}, Неявка={general.SystemTotals.AbsenceTotal}, Всего={general.SystemTotals.GrandTotal}")
                            .Bold();
                    });
                });

                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content().Column(column =>
                    {
                        column.Item().Text($"Статистика проф. осмотров за {GetMonthName(month)} {year}")
                            .FontSize(18).Bold().AlignCenter();

                        column.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(2.2f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(1.2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Филиал").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Администратор").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Ставка").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Пригласили").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Записались").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Пришли").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Категория").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Премия").Bold();
                            });

                            foreach (var item in profo.Rows)
                            {
                                table.Cell().Border(1).Padding(4).Text(item.BranchName);
                                table.Cell().Border(1).Padding(4).Text(item.EmployeeFullName);
                                table.Cell().Border(1).Padding(4).Text(item.Rate?.ToString() ?? "");
                                table.Cell().Border(1).Padding(4).Text(item.InvitedCount.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.BookedCount.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.ArrivedCount.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.ProfoCategoryName ?? "");
                                table.Cell().Border(1).Padding(4).Text(item.Premium.ToString("0.##"));
                            }

                            table.Cell().Border(1).Padding(4).Text("Итого").Bold();
                            table.Cell().Border(1).Padding(4).Text("").Bold();
                            table.Cell().Border(1).Padding(4).Text("").Bold();
                            table.Cell().Border(1).Padding(4).Text(profo.InvitedTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(profo.BookedTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(profo.ArrivedTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text("").Bold();
                            table.Cell().Border(1).Padding(4).Text(profo.PremiumTotal.ToString("0.##")).Bold();
                        });

                        column.Item().PaddingTop(10).Text($"Конверсия Пригласили→Записались: {profo.ConversionInvitedToBooked:0.#}%");
                        column.Item().Text($"Конверсия Записались→Пришли: {profo.ConversionBookedToArrived:0.#}%");
                        column.Item().Text($"Конверсия Пригласили→Пришли: {profo.ConversionInvitedToArrived:0.#}%");
                    });
                });

                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content().Column(column =>
                    {
                        column.Item().Text($"Статистика по администраторам за {GetMonthName(month)} {year}")
                            .FontSize(18).Bold().AlignCenter();

                        column.Item().PaddingTop(10).Text("ПО ФИЛИАЛАМ").Bold().FontSize(12);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Филиал").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Администратор").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Явка").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Неявка").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Премия").Bold();
                            });

                            foreach (var item in admin.BranchRows)
                            {
                                table.Cell().Border(1).Padding(4).Text(item.BranchName);
                                table.Cell().Border(1).Padding(4).Text(item.EmployeeFullName);
                                table.Cell().Border(1).Padding(4).Text(item.AttendanceCount.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.AbsenceCount.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Premium.ToString("0.##"));
                            }

                            table.Cell().Border(1).Padding(4).Text("Итого по филиалам").Bold();
                            table.Cell().Border(1).Padding(4).Text("").Bold();
                            table.Cell().Border(1).Padding(4).Text(admin.BranchAttendanceTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(admin.BranchAbsenceTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(admin.BranchPremiumTotal.ToString("0.##")).Bold();
                        });

                        column.Item().PaddingTop(15).Text("КОЛЛ-ЦЕНТР").Bold().FontSize(12);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.2f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Филиал").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Оператор").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Явка").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Неявка").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Премия").Bold();
                            });

                            foreach (var item in admin.CallCenterRows)
                            {
                                table.Cell().Border(1).Padding(4).Text(item.BranchName);
                                table.Cell().Border(1).Padding(4).Text(item.EmployeeFullName);
                                table.Cell().Border(1).Padding(4).Text(item.AttendanceCount.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.AbsenceCount.ToString());
                                table.Cell().Border(1).Padding(4).Text(item.Premium.ToString("0.##"));
                            }

                            table.Cell().Border(1).Padding(4).Text("Итого по колл-центру").Bold();
                            table.Cell().Border(1).Padding(4).Text("").Bold();
                            table.Cell().Border(1).Padding(4).Text(admin.CallCenterAttendanceTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text(admin.CallCenterAbsenceTotal.ToString()).Bold();
                            table.Cell().Border(1).Padding(4).Text("").Bold();
                        });

                        column.Item().PaddingTop(10)
                            .Text($"Всего по системе: Явка={admin.SystemAttendanceTotal}, Неявка={admin.SystemAbsenceTotal}, Премия={admin.SystemPremiumTotal:0.##}")
                            .Bold();
                    });
                });
            }).GeneratePdf(filePath);
        }

        private string GetMonthName(int month)
        {
            return month switch
            {
                1 => "Январь",
                2 => "Февраль",
                3 => "Март",
                4 => "Апрель",
                5 => "Май",
                6 => "Июнь",
                7 => "Июль",
                8 => "Август",
                9 => "Сентябрь",
                10 => "Октябрь",
                11 => "Ноябрь",
                12 => "Декабрь",
                _ => "Неизвестный месяц"
            };
        }
    }
}