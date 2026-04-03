using ClinicStatisticsApp.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class BranchReportExcelExportService
    {
        public void Export(string filePath, int branchId, string branchName, int year, int month)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.PerkEntries).ThenInclude(x => x.Employee)
                .Include(r => r.ProfiEntries).ThenInclude(x => x.Employee)
                .Include(r => r.HoursEntries).ThenInclude(x => x.Employee)
                .Include(r => r.ReviewEntries).ThenInclude(x => x.Employee)
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == year && r.Month == month);

            if (report == null)
                throw new Exception("Отчет за выбранный период не найден.");

            using var workbook = new XLWorkbook();

            ExportPerk(workbook, report, branchName, year, month);
            ExportProfi(workbook, report, branchName, year, month);
            ExportHours(workbook, report, branchName, year, month);
            ExportReviews(workbook, report, branchName, year, month);

            workbook.SaveAs(filePath);
        }

        private void ExportPerk(XLWorkbook workbook, Models.BranchReport report, string branchName, int year, int month)
        {
            var ws = workbook.Worksheets.Add("ПЕРК");

            ws.Cell(1, 1).Value = $"Филиал: {branchName}";
            ws.Cell(2, 1).Value = $"Период: {GetMonthName(month)} {year}";
            ws.Cell(4, 1).Value = "ФИО администратора";
            ws.Cell(4, 2).Value = "Явка";
            ws.Cell(4, 3).Value = "Неявка";
            ws.Cell(4, 4).Value = "Всего";

            int row = 5;
            foreach (var item in report.PerkEntries.OrderBy(x => x.Employee!.FullName))
            {
                ws.Cell(row, 1).Value = item.Employee!.FullName;
                ws.Cell(row, 2).Value = item.AttendanceCount;
                ws.Cell(row, 3).Value = item.AbsenceCount;
                ws.Cell(row, 4).Value = item.AttendanceCount + item.AbsenceCount;
                row++;
            }

            ws.Cell(row, 1).Value = "Итого";
            ws.Cell(row, 2).Value = report.PerkEntries.Sum(x => x.AttendanceCount);
            ws.Cell(row, 3).Value = report.PerkEntries.Sum(x => x.AbsenceCount);
            ws.Cell(row, 4).Value = report.PerkEntries.Sum(x => x.AttendanceCount + x.AbsenceCount);

            FormatWorksheet(ws, row, 4);
        }

        private void ExportProfi(XLWorkbook workbook, Models.BranchReport report, string branchName, int year, int month)
        {
            var ws = workbook.Worksheets.Add("ПРОФЫ");

            ws.Cell(1, 1).Value = $"Филиал: {branchName}";
            ws.Cell(2, 1).Value = $"Период: {GetMonthName(month)} {year}";
            ws.Cell(4, 1).Value = "ФИО администратора";
            ws.Cell(4, 2).Value = "Пригласили";
            ws.Cell(4, 3).Value = "Записались";
            ws.Cell(4, 4).Value = "Пришли";

            int row = 5;
            foreach (var item in report.ProfiEntries.OrderBy(x => x.Employee!.FullName))
            {
                ws.Cell(row, 1).Value = item.Employee!.FullName;
                ws.Cell(row, 2).Value = item.InvitedCount;
                ws.Cell(row, 3).Value = item.BookedCount;
                ws.Cell(row, 4).Value = item.ArrivedCount;
                row++;
            }

            ws.Cell(row, 1).Value = "Итого";
            ws.Cell(row, 2).Value = report.ProfiEntries.Sum(x => x.InvitedCount);
            ws.Cell(row, 3).Value = report.ProfiEntries.Sum(x => x.BookedCount);
            ws.Cell(row, 4).Value = report.ProfiEntries.Sum(x => x.ArrivedCount);

            FormatWorksheet(ws, row, 4);
        }

        private void ExportHours(XLWorkbook workbook, Models.BranchReport report, string branchName, int year, int month)
        {
            var ws = workbook.Worksheets.Add("ЧАСЫ");

            ws.Cell(1, 1).Value = $"Филиал: {branchName}";
            ws.Cell(2, 1).Value = $"Период: {GetMonthName(month)} {year}";
            ws.Cell(4, 1).Value = "ФИО администратора";
            ws.Cell(4, 2).Value = "Отработанные часы";

            int row = 5;
            foreach (var item in report.HoursEntries.OrderBy(x => x.Employee!.FullName))
            {
                ws.Cell(row, 1).Value = item.Employee!.FullName;
                ws.Cell(row, 2).Value = item.WorkedHours;
                row++;
            }

            ws.Cell(row, 1).Value = "Итого";
            ws.Cell(row, 2).Value = report.HoursEntries.Sum(x => x.WorkedHours);

            FormatWorksheet(ws, row, 2);
        }

        private void ExportReviews(XLWorkbook workbook, Models.BranchReport report, string branchName, int year, int month)
        {
            var ws = workbook.Worksheets.Add("ОТЗЫВЫ");

            ws.Cell(1, 1).Value = $"Филиал: {branchName}";
            ws.Cell(2, 1).Value = $"Период: {GetMonthName(month)} {year}";
            ws.Cell(4, 1).Value = "ФИО администратора";
            ws.Cell(4, 2).Value = "Отправлено СМС";
            ws.Cell(4, 3).Value = "Оставлено отзывов";

            int row = 5;
            foreach (var item in report.ReviewEntries.OrderBy(x => x.Employee!.FullName))
            {
                ws.Cell(row, 1).Value = item.Employee!.FullName;
                ws.Cell(row, 2).Value = item.SmsSentCount;
                ws.Cell(row, 3).Value = item.ReviewsLeftCount;
                row++;
            }

            ws.Cell(row, 1).Value = "Итого";
            ws.Cell(row, 2).Value = report.ReviewEntries.Sum(x => x.SmsSentCount);
            ws.Cell(row, 3).Value = report.ReviewEntries.Sum(x => x.ReviewsLeftCount);

            FormatWorksheet(ws, row, 3);
        }

        private void FormatWorksheet(IXLWorksheet ws, int lastRow, int lastColumn)
        {
            ws.Range(4, 1, 4, lastColumn).Style.Font.Bold = true;
            ws.Range(4, 1, 4, lastColumn).Style.Fill.BackgroundColor = XLColor.LightGray;

            ws.Range(4, 1, lastRow, lastColumn).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(4, 1, lastRow, lastColumn).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            ws.Cell(lastRow, 1).Style.Font.Bold = true;
            for (int col = 2; col <= lastColumn; col++)
            {
                ws.Cell(lastRow, col).Style.Font.Bold = true;
            }

            ws.Columns().AdjustToContents();
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