using ClinicStatisticsApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class NaradPdfExportService
    {
        public void Export(string filePath, string branchName, int year, int month, List<NaradEntryViewModel> items)
        {
            var includedItems = items.Where(x => x.IsIncluded).ToList();
            var monthName = GetMonthName(month);

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Content().Column(column =>
                    {
                        column.Item().Text("Наряд по мотивационной оплате")
                            .FontSize(18)
                            .Bold()
                            .AlignCenter();

                        column.Item().PaddingTop(5).Text($"Филиал: {branchName}");
                        column.Item().Text($"Период: {monthName} {year}");

                        column.Item().PaddingTop(20).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3); // ФИО
                                columns.RelativeColumn(1.5f); // СМС
                                columns.RelativeColumn(1.5f); // отзывы
                                columns.RelativeColumn(1.5f); // ставка
                                columns.RelativeColumn(1.5f); // сумма
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("ФИО администратора").Bold();
                                header.Cell().Element(CellStyle).Text("Отправлено СМС").Bold();
                                header.Cell().Element(CellStyle).Text("Оставлено отзывов").Bold();
                                header.Cell().Element(CellStyle).Text("Оплата за 1 отзыв").Bold();
                                header.Cell().Element(CellStyle).Text("Сумма к оплате").Bold();
                            });

                            foreach (var item in includedItems)
                            {
                                table.Cell().Element(CellStyle).Text(item.EmployeeFullName);
                                table.Cell().Element(CellStyle).Text(item.SmsSentCount.ToString());
                                table.Cell().Element(CellStyle).Text(item.ReviewsLeftCount.ToString());
                                table.Cell().Element(CellStyle).Text(item.PaymentPerReview.ToString("0.##"));
                                table.Cell().Element(CellStyle).Text(item.TotalPayment.ToString("0.##"));
                            }

                            table.Cell().Element(CellStyle).Text("Итого").Bold();
                            table.Cell().Element(CellStyle).Text(includedItems.Sum(x => x.SmsSentCount).ToString()).Bold();
                            table.Cell().Element(CellStyle).Text(includedItems.Sum(x => x.ReviewsLeftCount).ToString()).Bold();
                            table.Cell().Element(CellStyle).Text("");
                            table.Cell().Element(CellStyle).Text(includedItems.Sum(x => x.TotalPayment).ToString("0.##")).Bold();
                        });
                    });
                });
            }).GeneratePdf(filePath);
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten1)
                .Padding(5);
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