using ClinicStatisticsApp.Models;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class NaradExcelExportService
    {
        public void Export(string filePath, string branchName, int year, int month, List<NaradEntryViewModel> items)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Наряд");

            var includedItems = items.Where(x => x.IsIncluded).ToList();

            string monthName = GetMonthName(month);

            worksheet.Cell(1, 1).Value = "Наряд по мотивационной оплате";
            worksheet.Cell(2, 1).Value = $"Филиал: {branchName}";
            worksheet.Cell(3, 1).Value = $"Период: {monthName} {year}";
            worksheet.Cell(5, 1).Value = "ФИО администратора";
            worksheet.Cell(5, 2).Value = "Отправлено СМС";
            worksheet.Cell(5, 3).Value = "Оставлено отзывов";
            worksheet.Cell(5, 4).Value = "Оплата за 1 отзыв";
            worksheet.Cell(5, 5).Value = "Сумма к оплате";

            int row = 6;

            foreach (var item in includedItems)
            {
                worksheet.Cell(row, 1).Value = item.EmployeeFullName;
                worksheet.Cell(row, 2).Value = item.SmsSentCount;
                worksheet.Cell(row, 3).Value = item.ReviewsLeftCount;
                worksheet.Cell(row, 4).Value = item.PaymentPerReview;
                worksheet.Cell(row, 5).Value = item.TotalPayment;
                row++;
            }

            worksheet.Cell(row, 1).Value = "Итого";
            worksheet.Cell(row, 2).Value = includedItems.Sum(x => x.SmsSentCount);
            worksheet.Cell(row, 3).Value = includedItems.Sum(x => x.ReviewsLeftCount);
            worksheet.Cell(row, 5).Value = includedItems.Sum(x => x.TotalPayment);

            var headerRange = worksheet.Range(5, 1, 5, 5);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var dataRange = worksheet.Range(5, 1, row, 5);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            worksheet.Range(1, 1, 3, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Style.Font.Bold = true;
            worksheet.Cell(row, 5).Style.Font.Bold = true;

            worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
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