using ClinicStatisticsApp.Models;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class SummaryBookExcelExportService
    {
        private readonly SummaryGeneralService _summaryGeneralService = new SummaryGeneralService();
        private readonly SummaryProfoService _summaryProfoService = new SummaryProfoService();
        private readonly SummaryAdminService _summaryAdminService = new SummaryAdminService();
        private readonly SummaryProDoctorService _summaryProDoctorService = new SummaryProDoctorService();
        private readonly DynamicsService _dynamicsService = new DynamicsService();
        private readonly ComparativePerkService _comparativePerkService = new ComparativePerkService();
        private readonly ComparativeProfiService _comparativeProfiService = new ComparativeProfiService();
        private readonly AbsolutePrimaryService _absolutePrimaryService = new AbsolutePrimaryService();

        public void ExportMonthlySummaryBook(string filePath, int year, int month)
        {
            using var workbook = new XLWorkbook();

            ExportSummaryGeneral(workbook, year, month);
            ExportSummaryProfo(workbook, year, month);
            ExportSummaryAdmin(workbook, year, month);

            // Этап 2
            ExportSummaryProDoctor(workbook, year);
            ExportDynamics(workbook, year);
            ExportComparativePerk(workbook, year, new List<int> { year - 1, year - 2 });
            ExportComparativeProfi(workbook, year, new List<int> { year - 1, year - 2 });
            ExportAbsolutePrimary(workbook, year);

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

        private void ExportSummaryProDoctor(XLWorkbook workbook, int year)
        {
            var data = _summaryProDoctorService.Build(year);
            var ws = workbook.Worksheets.Add("Отзывы ПроДокторов");

            int row = 1;
            ws.Cell(row, 1).Value = $"Отзывы ПроДокторов за {year} год";
            row += 2;

            foreach (var block in data.BranchBlocks)
            {
                ws.Cell(row, 1).Value = block.BranchName;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 16;
                row++;

                WriteYearHeader(ws, row, "Администратор");
                row++;

                foreach (var emp in block.Employees)
                {
                    ws.Cell(row, 1).Value = emp.EmployeeFullName;
                    WriteMonths(ws, row, emp.January, emp.February, emp.March, emp.April, emp.May, emp.June,
                        emp.July, emp.August, emp.September, emp.October, emp.November, emp.December);
                    row++;
                }

                ws.Cell(row, 1).Value = "QR-код";
                WriteMonths(ws, row, block.QrJanuary, block.QrFebruary, block.QrMarch, block.QrApril, block.QrMay, block.QrJune,
                    block.QrJuly, block.QrAugust, block.QrSeptember, block.QrOctober, block.QrNovember, block.QrDecember);
                row++;

                ws.Cell(row, 1).Value = "Итого";
                WriteMonths(ws, row, block.TotalJanuary, block.TotalFebruary, block.TotalMarch, block.TotalApril, block.TotalMay, block.TotalJune,
                    block.TotalJuly, block.TotalAugust, block.TotalSeptember, block.TotalOctober, block.TotalNovember, block.TotalDecember);
                ws.Range(row, 1, row, 13).Style.Font.Bold = true;

                row += 2;
            }

            ws.Cell(row, 1).Value = "Итого по филиалам";
            WriteMonths(ws, row,
                data.GrandTotalJanuary, data.GrandTotalFebruary, data.GrandTotalMarch, data.GrandTotalApril,
                data.GrandTotalMay, data.GrandTotalJune, data.GrandTotalJuly, data.GrandTotalAugust,
                data.GrandTotalSeptember, data.GrandTotalOctober, data.GrandTotalNovember, data.GrandTotalDecember);
            ws.Range(row, 1, row, 13).Style.Font.Bold = true;

            ws.Columns().AdjustToContents();
        }

        private void ExportDynamics(XLWorkbook workbook, int year)
        {
            var data = _dynamicsService.Build(year, new List<int> { year - 1, year - 2 });
            var ws = workbook.Worksheets.Add("Динамика");

            int row = 1;
            ws.Cell(row, 1).Value = $"Динамика за {year} год";
            row += 2;

            foreach (var block in data.BranchBlocks)
            {
                ws.Cell(row, 1).Value = block.BranchName;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 16;
                row++;

                WriteYearHeader(ws, row, "Сотрудник");
                row++;

                foreach (var emp in block.Employees)
                {
                    ws.Cell(row, 1).Value = emp.EmployeeFullName;
                    WriteMonths(ws, row, emp.January, emp.February, emp.March, emp.April, emp.May, emp.June,
                        emp.July, emp.August, emp.September, emp.October, emp.November, emp.December);
                    row++;
                }

                ws.Cell(row, 1).Value = "Итого";
                WriteMonths(ws, row, block.TotalJanuary, block.TotalFebruary, block.TotalMarch, block.TotalApril, block.TotalMay, block.TotalJune,
                    block.TotalJuly, block.TotalAugust, block.TotalSeptember, block.TotalOctober, block.TotalNovember, block.TotalDecember);
                ws.Range(row, 1, row, 13).Style.Font.Bold = true;

                row += 2;
            }

            if (data.CallCenterBlock != null)
            {
                var block = data.CallCenterBlock;

                ws.Cell(row, 1).Value = block.BranchName;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 16;
                row++;

                WriteYearHeader(ws, row, "Сотрудник");
                row++;

                foreach (var emp in block.Employees)
                {
                    ws.Cell(row, 1).Value = emp.EmployeeFullName;
                    WriteMonths(ws, row, emp.January, emp.February, emp.March, emp.April, emp.May, emp.June,
                        emp.July, emp.August, emp.September, emp.October, emp.November, emp.December);
                    row++;
                }

                ws.Cell(row, 1).Value = "Итого";
                WriteMonths(ws, row, block.TotalJanuary, block.TotalFebruary, block.TotalMarch, block.TotalApril, block.TotalMay, block.TotalJune,
                    block.TotalJuly, block.TotalAugust, block.TotalSeptember, block.TotalOctober, block.TotalNovember, block.TotalDecember);
                ws.Range(row, 1, row, 13).Style.Font.Bold = true;

                row += 3;
            }

            ws.Cell(row, 1).Value = "Сравнение: только филиалы";
            row++;
            ExportComparisonRows(ws, ref row, data.BranchComparisonRows);

            row++;
            ws.Cell(row, 1).Value = "Сравнение: только колл-центр";
            row++;
            ExportComparisonRows(ws, ref row, data.CallCenterComparisonRows);

            row++;
            ws.Cell(row, 1).Value = "Сравнение: филиалы + колл-центр";
            row++;
            ExportComparisonRows(ws, ref row, data.SystemComparisonRows);

            ws.Columns().AdjustToContents();
        }

        private void ExportComparativePerk(XLWorkbook workbook, int year, List<int> otherYears)
        {
            var data = _comparativePerkService.Build(year, otherYears);
            var ws = workbook.Worksheets.Add("Сравнительная ПЕРК");

            WriteComparativeSheet(ws, data, $"Сравнительная статистика ПЕРК за {year}");
        }

        private void ExportComparativeProfi(XLWorkbook workbook, int year, List<int> otherYears)
        {
            var data = _comparativeProfiService.Build(year, otherYears);
            var ws = workbook.Worksheets.Add("Сравнительная ПРОФЫ");

            WriteComparativeSheet(ws, data, $"Сравнительная статистика ПРОФЫ за {year}");
        }

        private void ExportAbsolutePrimary(XLWorkbook workbook, int year)
        {
            var data = _absolutePrimaryService.Build(year);
            var ws = workbook.Worksheets.Add("Абсолютные первичные");

            ws.Cell(1, 1).Value = $"Статистика абсолютных первичных за {year}";
            int row = 3;

            ws.Cell(row, 1).Value = "Филиал";
            int col = 2;

            WriteTripleMonthHeader(ws, row, ref col, "Янв");
            WriteTripleMonthHeader(ws, row, ref col, "Фев");
            WriteTripleMonthHeader(ws, row, ref col, "Мар");
            WriteTripleMonthHeader(ws, row, ref col, "Апр");
            WriteTripleMonthHeader(ws, row, ref col, "Май");
            WriteTripleMonthHeader(ws, row, ref col, "Июн");
            WriteTripleMonthHeader(ws, row, ref col, "Июл");
            WriteTripleMonthHeader(ws, row, ref col, "Авг");
            WriteTripleMonthHeader(ws, row, ref col, "Сен");
            WriteTripleMonthHeader(ws, row, ref col, "Окт");
            WriteTripleMonthHeader(ws, row, ref col, "Ноя");
            WriteTripleMonthHeader(ws, row, ref col, "Дек");

            ws.Range(row, 1, row, col - 1).Style.Font.Bold = true;
            ws.Range(row, 1, row, col - 1).Style.Fill.BackgroundColor = XLColor.LightGray;

            row++;

            foreach (var item in data.Rows)
            {
                col = 1;
                ws.Cell(row, col++).Value = item.BranchName;

                WriteTripleMonthValues(ws, row, ref col, item.JanuaryBranch, item.JanuaryCallCenter, item.JanuaryTotal);
                WriteTripleMonthValues(ws, row, ref col, item.FebruaryBranch, item.FebruaryCallCenter, item.FebruaryTotal);
                WriteTripleMonthValues(ws, row, ref col, item.MarchBranch, item.MarchCallCenter, item.MarchTotal);
                WriteTripleMonthValues(ws, row, ref col, item.AprilBranch, item.AprilCallCenter, item.AprilTotal);
                WriteTripleMonthValues(ws, row, ref col, item.MayBranch, item.MayCallCenter, item.MayTotal);
                WriteTripleMonthValues(ws, row, ref col, item.JuneBranch, item.JuneCallCenter, item.JuneTotal);
                WriteTripleMonthValues(ws, row, ref col, item.JulyBranch, item.JulyCallCenter, item.JulyTotal);
                WriteTripleMonthValues(ws, row, ref col, item.AugustBranch, item.AugustCallCenter, item.AugustTotal);
                WriteTripleMonthValues(ws, row, ref col, item.SeptemberBranch, item.SeptemberCallCenter, item.SeptemberTotal);
                WriteTripleMonthValues(ws, row, ref col, item.OctoberBranch, item.OctoberCallCenter, item.OctoberTotal);
                WriteTripleMonthValues(ws, row, ref col, item.NovemberBranch, item.NovemberCallCenter, item.NovemberTotal);
                WriteTripleMonthValues(ws, row, ref col, item.DecemberBranch, item.DecemberCallCenter, item.DecemberTotal);

                row++;
            }

            col = 1;
            ws.Cell(row, col++).Value = data.Totals.BranchName;
            WriteTripleMonthValues(ws, row, ref col, data.Totals.JanuaryBranch, data.Totals.JanuaryCallCenter, data.Totals.JanuaryTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.FebruaryBranch, data.Totals.FebruaryCallCenter, data.Totals.FebruaryTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.MarchBranch, data.Totals.MarchCallCenter, data.Totals.MarchTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.AprilBranch, data.Totals.AprilCallCenter, data.Totals.AprilTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.MayBranch, data.Totals.MayCallCenter, data.Totals.MayTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.JuneBranch, data.Totals.JuneCallCenter, data.Totals.JuneTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.JulyBranch, data.Totals.JulyCallCenter, data.Totals.JulyTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.AugustBranch, data.Totals.AugustCallCenter, data.Totals.AugustTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.SeptemberBranch, data.Totals.SeptemberCallCenter, data.Totals.SeptemberTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.OctoberBranch, data.Totals.OctoberCallCenter, data.Totals.OctoberTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.NovemberBranch, data.Totals.NovemberCallCenter, data.Totals.NovemberTotal);
            WriteTripleMonthValues(ws, row, ref col, data.Totals.DecemberBranch, data.Totals.DecemberCallCenter, data.Totals.DecemberTotal);

            ws.Range(row, 1, row, col - 1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();
        }

        private void WriteComparativeSheet(IXLWorksheet ws, ComparativePerkResult data, string title)
        {
            ws.Cell(1, 1).Value = title;

            ws.Cell(3, 1).Value = "Филиал";
            ws.Cell(3, 2).Value = "Январь";
            ws.Cell(3, 3).Value = "Февраль";
            ws.Cell(3, 4).Value = "Март";
            ws.Cell(3, 5).Value = "Апрель";
            ws.Cell(3, 6).Value = "Май";
            ws.Cell(3, 7).Value = "Июнь";
            ws.Cell(3, 8).Value = "Июль";
            ws.Cell(3, 9).Value = "Август";
            ws.Cell(3, 10).Value = "Сентябрь";
            ws.Cell(3, 11).Value = "Октябрь";
            ws.Cell(3, 12).Value = "Ноябрь";
            ws.Cell(3, 13).Value = "Декабрь";
            ws.Cell(3, 14).Value = $"Итог {data.MainYear}";

            int col = 15;
            foreach (var year in data.OtherYears)
            {
                ws.Cell(3, col).Value = $"Итог {year}";
                col++;
            }

            ws.Range(3, 1, 3, col - 1).Style.Font.Bold = true;
            ws.Range(3, 1, 3, col - 1).Style.Fill.BackgroundColor = XLColor.LightGray;

            int row = 4;
            foreach (var item in data.Rows)
            {
                ws.Cell(row, 1).Value = item.Name;
                ws.Cell(row, 2).Value = item.January;
                ws.Cell(row, 3).Value = item.February;
                ws.Cell(row, 4).Value = item.March;
                ws.Cell(row, 5).Value = item.April;
                ws.Cell(row, 6).Value = item.May;
                ws.Cell(row, 7).Value = item.June;
                ws.Cell(row, 8).Value = item.July;
                ws.Cell(row, 9).Value = item.August;
                ws.Cell(row, 10).Value = item.September;
                ws.Cell(row, 11).Value = item.October;
                ws.Cell(row, 12).Value = item.November;
                ws.Cell(row, 13).Value = item.December;
                ws.Cell(row, 14).Value = item.MainYearTotal;

                col = 15;
                foreach (var year in data.OtherYears)
                {
                    ws.Cell(row, col).Value = item.OtherYearTotals.ContainsKey(year) ? item.OtherYearTotals[year] : 0;
                    col++;
                }

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void ExportComparisonRows(IXLWorksheet ws, ref int row, List<DynamicsComparisonRowViewModel> rows)
        {
            ws.Cell(row, 1).Value = "Год";
            ws.Cell(row, 2).Value = "Январь";
            ws.Cell(row, 3).Value = "Февраль";
            ws.Cell(row, 4).Value = "Март";
            ws.Cell(row, 5).Value = "Апрель";
            ws.Cell(row, 6).Value = "Май";
            ws.Cell(row, 7).Value = "Июнь";
            ws.Cell(row, 8).Value = "Июль";
            ws.Cell(row, 9).Value = "Август";
            ws.Cell(row, 10).Value = "Сентябрь";
            ws.Cell(row, 11).Value = "Октябрь";
            ws.Cell(row, 12).Value = "Ноябрь";
            ws.Cell(row, 13).Value = "Декабрь";
            ws.Range(row, 1, row, 13).Style.Font.Bold = true;
            row++;

            foreach (var item in rows)
            {
                ws.Cell(row, 1).Value = item.Year;
                WriteMonths(ws, row, item.January, item.February, item.March, item.April, item.May, item.June,
                    item.July, item.August, item.September, item.October, item.November, item.December);
                row++;
            }
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

        private void WriteYearHeader(IXLWorksheet ws, int row, string firstColumnName)
        {
            ws.Cell(row, 1).Value = firstColumnName;
            ws.Cell(row, 2).Value = "Январь";
            ws.Cell(row, 3).Value = "Февраль";
            ws.Cell(row, 4).Value = "Март";
            ws.Cell(row, 5).Value = "Апрель";
            ws.Cell(row, 6).Value = "Май";
            ws.Cell(row, 7).Value = "Июнь";
            ws.Cell(row, 8).Value = "Июль";
            ws.Cell(row, 9).Value = "Август";
            ws.Cell(row, 10).Value = "Сентябрь";
            ws.Cell(row, 11).Value = "Октябрь";
            ws.Cell(row, 12).Value = "Ноябрь";
            ws.Cell(row, 13).Value = "Декабрь";

            ws.Range(row, 1, row, 13).Style.Font.Bold = true;
            ws.Range(row, 1, row, 13).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        private void WriteMonths(IXLWorksheet ws, int row,
            int jan, int feb, int mar, int apr, int may, int jun,
            int jul, int aug, int sep, int oct, int nov, int dec)
        {
            ws.Cell(row, 2).Value = jan;
            ws.Cell(row, 3).Value = feb;
            ws.Cell(row, 4).Value = mar;
            ws.Cell(row, 5).Value = apr;
            ws.Cell(row, 6).Value = may;
            ws.Cell(row, 7).Value = jun;
            ws.Cell(row, 8).Value = jul;
            ws.Cell(row, 9).Value = aug;
            ws.Cell(row, 10).Value = sep;
            ws.Cell(row, 11).Value = oct;
            ws.Cell(row, 12).Value = nov;
            ws.Cell(row, 13).Value = dec;
        }

        private void WriteTripleMonthHeader(IXLWorksheet ws, int row, ref int col, string monthName)
        {
            ws.Cell(row, col++).Value = $"{monthName} ФЛ";
            ws.Cell(row, col++).Value = $"{monthName} КЦ";
            ws.Cell(row, col++).Value = $"{monthName} Итого";
        }

        private void WriteTripleMonthValues(IXLWorksheet ws, int row, ref int col, int branch, int cc, int total)
        {
            ws.Cell(row, col++).Value = branch;
            ws.Cell(row, col++).Value = cc;
            ws.Cell(row, col++).Value = total;
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