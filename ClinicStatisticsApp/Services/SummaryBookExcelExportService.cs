using ClinicStatisticsApp.Models;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class SummaryBookExcelExportService
    {
        private readonly SummaryGeneralService _summaryGeneralService = new SummaryGeneralService();
        private readonly SummaryProfoService _summaryProfoService = new SummaryProfoService();
        private readonly SummaryAdminService _summaryAdminService = new SummaryAdminService();

        public void ExportMonthlySummaryBook(string filePath, int year, int month)
        {
            using var workbook = new XLWorkbook();

            ExportSummaryGeneral(workbook, year, month);
            ExportSummaryProfo(workbook, year, month);
            ExportSummaryAdmin(workbook, year, month);

            workbook.SaveAs(filePath);
        }

        private void ExportSummaryGeneral(XLWorkbook workbook, int year, int month)
        {
            var data = _summaryGeneralService.Build(year, month);
            var ws = workbook.Worksheets.Add("Статистика общая");

            ws.Cell(1, 1).Value = $"Статистика общая за {GetMonthName(month)} {year}";
            ws.Cell(3, 1).Value = "ФИЛИАЛЫ";

            int row = 5;
            WriteSummaryGeneralHeader(ws, row);
            row++;

            foreach (var item in data.BranchRows)
            {
                WriteSummaryGeneralRow(ws, row, item);
                row++;
            }

            ws.Cell(row, 1).Value = "Итого по филиалам";
            WriteSummaryGeneralTotals(ws, row, data.BranchTotals);
            row += 2;

            ws.Cell(row, 1).Value = "КОЛЛ-ЦЕНТР";
            row += 2;

            WriteSummaryGeneralHeader(ws, row);
            row++;

            foreach (var item in data.CallCenterRows)
            {
                WriteSummaryGeneralRow(ws, row, item);
                row++;
            }

            ws.Cell(row, 1).Value = "Итого по колл-центру";
            WriteSummaryGeneralTotals(ws, row, data.CallCenterTotals);
            row += 2;

            ws.Cell(row, 1).Value = "Всего по системе клиник";
            WriteSummaryGeneralTotals(ws, row, data.SystemTotals);

            ws.Columns().AdjustToContents();
        }

        private void WriteSummaryGeneralHeader(IXLWorksheet ws, int row)
        {
            ws.Cell(row, 1).Value = "№";
            ws.Cell(row, 2).Value = "Сотрудник";
            ws.Cell(row, 3).Value = "Явка";
            ws.Cell(row, 4).Value = "Неявка";
            ws.Cell(row, 5).Value = "Всего";
            ws.Cell(row, 6).Value = "ЦК";
            ws.Cell(row, 7).Value = "Комфорт";
            ws.Cell(row, 8).Value = "Баграмяна";
            ws.Cell(row, 9).Value = "Детство";
            ws.Cell(row, 10).Value = "Генделя";
            ws.Cell(row, 11).Value = "Виктория";
            ws.Cell(row, 12).Value = "Альфа";
            ws.Cell(row, 13).Value = "Регион";
            ws.Cell(row, 14).Value = "Артиллерийская";
            ws.Cell(row, 15).Value = "Сельма";

            var range = ws.Range(row, 1, row, 15);
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.LightGray;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private void WriteSummaryGeneralRow(IXLWorksheet ws, int row, SummaryGeneralRowViewModel item)
        {
            ws.Cell(row, 1).Value = item.Number;
            ws.Cell(row, 2).Value = item.EmployeeFullName;
            ws.Cell(row, 3).Value = item.AttendanceTotal;
            ws.Cell(row, 4).Value = item.AbsenceTotal;
            ws.Cell(row, 5).Value = item.GrandTotal;
            ws.Cell(row, 6).Value = item.Ck;
            ws.Cell(row, 7).Value = item.Comfort;
            ws.Cell(row, 8).Value = item.Bagramyana;
            ws.Cell(row, 9).Value = item.Detstvo;
            ws.Cell(row, 10).Value = item.Gendelya;
            ws.Cell(row, 11).Value = item.Viktoriya;
            ws.Cell(row, 12).Value = item.Alfa;
            ws.Cell(row, 13).Value = item.Region;
            ws.Cell(row, 14).Value = item.Artilleriyskaya;
            ws.Cell(row, 15).Value = item.Selma;

            ws.Range(row, 1, row, 15).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 15).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private void WriteSummaryGeneralTotals(IXLWorksheet ws, int row, SummaryGeneralTotalsViewModel totals)
        {
            ws.Cell(row, 3).Value = totals.AttendanceTotal;
            ws.Cell(row, 4).Value = totals.AbsenceTotal;
            ws.Cell(row, 5).Value = totals.GrandTotal;
            ws.Cell(row, 6).Value = totals.Ck;
            ws.Cell(row, 7).Value = totals.Comfort;
            ws.Cell(row, 8).Value = totals.Bagramyana;
            ws.Cell(row, 9).Value = totals.Detstvo;
            ws.Cell(row, 10).Value = totals.Gendelya;
            ws.Cell(row, 11).Value = totals.Viktoriya;
            ws.Cell(row, 12).Value = totals.Alfa;
            ws.Cell(row, 13).Value = totals.Region;
            ws.Cell(row, 14).Value = totals.Artilleriyskaya;
            ws.Cell(row, 15).Value = totals.Selma;

            ws.Range(row, 1, row, 15).Style.Font.Bold = true;
            ws.Range(row, 1, row, 15).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(row, 1, row, 15).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private void ExportSummaryProfo(XLWorkbook workbook, int year, int month)
        {
            var data = _summaryProfoService.Build(year, month);
            var ws = workbook.Worksheets.Add("Статистика профосмотров");

            ws.Cell(1, 1).Value = $"Статистика проф. осмотров за {GetMonthName(month)} {year}";
            ws.Cell(3, 1).Value = "Филиал";
            ws.Cell(3, 2).Value = "Администратор";
            ws.Cell(3, 3).Value = "Ставка";
            ws.Cell(3, 4).Value = "Пригласили";
            ws.Cell(3, 5).Value = "Записались";
            ws.Cell(3, 6).Value = "Пришли";
            ws.Cell(3, 7).Value = "Категория";
            ws.Cell(3, 8).Value = "Премия";

            var headerRange = ws.Range(3, 1, 3, 8);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = 4;
            foreach (var item in data.Rows)
            {
                ws.Cell(row, 1).Value = item.BranchName;
                ws.Cell(row, 2).Value = item.EmployeeFullName;
                ws.Cell(row, 3).Value = item.Rate;
                ws.Cell(row, 4).Value = item.InvitedCount;
                ws.Cell(row, 5).Value = item.BookedCount;
                ws.Cell(row, 6).Value = item.ArrivedCount;
                ws.Cell(row, 7).Value = item.ProfoCategoryName;
                ws.Cell(row, 8).Value = item.Premium;

                ws.Range(row, 1, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                row++;
            }

            ws.Cell(row, 1).Value = "Итого";
            ws.Cell(row, 4).Value = data.InvitedTotal;
            ws.Cell(row, 5).Value = data.BookedTotal;
            ws.Cell(row, 6).Value = data.ArrivedTotal;
            ws.Cell(row, 8).Value = data.PremiumTotal;

            ws.Range(row, 1, row, 8).Style.Font.Bold = true;
            row += 2;

            ws.Cell(row, 1).Value = $"Конверсия Пригласили→Записались: {data.ConversionInvitedToBooked:0.#}%";
            row++;
            ws.Cell(row, 1).Value = $"Конверсия Записались→Пришли: {data.ConversionBookedToArrived:0.#}%";
            row++;
            ws.Cell(row, 1).Value = $"Конверсия Пригласили→Пришли: {data.ConversionInvitedToArrived:0.#}%";

            ws.Columns().AdjustToContents();
        }

        private void ExportSummaryAdmin(XLWorkbook workbook, int year, int month)
        {
            var data = _summaryAdminService.Build(year, month);
            var ws = workbook.Worksheets.Add("Статистика администраторов");

            ws.Cell(1, 1).Value = $"Статистика по администраторам за {GetMonthName(month)} {year}";

            ws.Cell(3, 1).Value = "ПО ФИЛИАЛАМ";
            ws.Cell(5, 1).Value = "Филиал";
            ws.Cell(5, 2).Value = "Администратор";
            ws.Cell(5, 3).Value = "Явка";
            ws.Cell(5, 4).Value = "Неявка";
            ws.Cell(5, 5).Value = "Премия";

            var branchHeader = ws.Range(5, 1, 5, 5);
            branchHeader.Style.Font.Bold = true;
            branchHeader.Style.Fill.BackgroundColor = XLColor.LightGray;
            branchHeader.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            branchHeader.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = 6;
            foreach (var item in data.BranchRows)
            {
                ws.Cell(row, 1).Value = item.BranchName;
                ws.Cell(row, 2).Value = item.EmployeeFullName;
                ws.Cell(row, 3).Value = item.AttendanceCount;
                ws.Cell(row, 4).Value = item.AbsenceCount;
                ws.Cell(row, 5).Value = item.Premium;

                ws.Range(row, 1, row, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 5).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                row++;
            }

            ws.Cell(row, 1).Value = "Итого по филиалам";
            ws.Cell(row, 3).Value = data.BranchAttendanceTotal;
            ws.Cell(row, 4).Value = data.BranchAbsenceTotal;
            ws.Cell(row, 5).Value = data.BranchPremiumTotal;
            ws.Range(row, 1, row, 5).Style.Font.Bold = true;

            row += 3;

            ws.Cell(row, 1).Value = "КОЛЛ-ЦЕНТР";
            row += 2;

            ws.Cell(row, 1).Value = "Филиал";
            ws.Cell(row, 2).Value = "Оператор";
            ws.Cell(row, 3).Value = "Явка";
            ws.Cell(row, 4).Value = "Неявка";
            ws.Cell(row, 5).Value = "Премия";

            var ccHeader = ws.Range(row, 1, row, 5);
            ccHeader.Style.Font.Bold = true;
            ccHeader.Style.Fill.BackgroundColor = XLColor.LightGray;
            ccHeader.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ccHeader.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            row++;

            foreach (var item in data.CallCenterRows)
            {
                ws.Cell(row, 1).Value = item.BranchName;
                ws.Cell(row, 2).Value = item.EmployeeFullName;
                ws.Cell(row, 3).Value = item.AttendanceCount;
                ws.Cell(row, 4).Value = item.AbsenceCount;
                ws.Cell(row, 5).Value = item.Premium;

                ws.Range(row, 1, row, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Range(row, 1, row, 5).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                row++;
            }

            ws.Cell(row, 1).Value = "Итого по колл-центру";
            ws.Cell(row, 3).Value = data.CallCenterAttendanceTotal;
            ws.Cell(row, 4).Value = data.CallCenterAbsenceTotal;
            ws.Range(row, 1, row, 5).Style.Font.Bold = true;

            row += 2;

            ws.Cell(row, 1).Value = "Всего по системе";
            ws.Cell(row, 3).Value = data.SystemAttendanceTotal;
            ws.Cell(row, 4).Value = data.SystemAbsenceTotal;
            ws.Cell(row, 5).Value = data.SystemPremiumTotal;
            ws.Range(row, 1, row, 5).Style.Font.Bold = true;

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