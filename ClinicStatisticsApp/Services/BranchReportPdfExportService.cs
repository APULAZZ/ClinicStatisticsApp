using ClinicStatisticsApp.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class BranchReportPdfExportService
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

            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(25);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content().Column(column =>
                    {
                        column.Item().Text("Филиальный отчет")
                            .FontSize(18)
                            .Bold()
                            .AlignCenter();

                        column.Item().PaddingTop(5).Text($"Филиал: {branchName}");
                        column.Item().Text($"Период: {GetMonthName(month)} {year}");

                        column.Item().PaddingTop(15).Text("ПЕРК").Bold().FontSize(14);
                        column.Item().Element(c => BuildPerkTable(c, report));

                        column.Item().PaddingTop(15).Text("ПРОФЫ").Bold().FontSize(14);
                        column.Item().Element(c => BuildProfiTable(c, report));

                        column.Item().PaddingTop(15).Text("ЧАСЫ").Bold().FontSize(14);
                        column.Item().Element(c => BuildHoursTable(c, report));

                        column.Item().PaddingTop(15).Text("ОТЗЫВЫ").Bold().FontSize(14);
                        column.Item().Element(c => BuildReviewsTable(c, report));
                    });
                });
            }).GeneratePdf(filePath);
        }

        private void BuildPerkTable(IContainer container, Models.BranchReport report)
        {
            var items = report.PerkEntries.OrderBy(x => x.Employee!.FullName).ToList();

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("ФИО администратора").Bold();
                    header.Cell().Element(CellStyle).Text("Явка").Bold();
                    header.Cell().Element(CellStyle).Text("Неявка").Bold();
                    header.Cell().Element(CellStyle).Text("Всего").Bold();
                });

                foreach (var item in items)
                {
                    table.Cell().Element(CellStyle).Text(item.Employee!.FullName);
                    table.Cell().Element(CellStyle).Text(item.AttendanceCount.ToString());
                    table.Cell().Element(CellStyle).Text(item.AbsenceCount.ToString());
                    table.Cell().Element(CellStyle).Text((item.AttendanceCount + item.AbsenceCount).ToString());
                }

                table.Cell().Element(CellStyle).Text("Итого").Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.AttendanceCount).ToString()).Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.AbsenceCount).ToString()).Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.AttendanceCount + x.AbsenceCount).ToString()).Bold();
            });
        }

        private void BuildProfiTable(IContainer container, Models.BranchReport report)
        {
            var items = report.ProfiEntries.OrderBy(x => x.Employee!.FullName).ToList();

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1.3f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("ФИО администратора").Bold();
                    header.Cell().Element(CellStyle).Text("Пригласили").Bold();
                    header.Cell().Element(CellStyle).Text("Записались").Bold();
                    header.Cell().Element(CellStyle).Text("Пришли").Bold();
                });

                foreach (var item in items)
                {
                    table.Cell().Element(CellStyle).Text(item.Employee!.FullName);
                    table.Cell().Element(CellStyle).Text(item.InvitedCount.ToString());
                    table.Cell().Element(CellStyle).Text(item.BookedCount.ToString());
                    table.Cell().Element(CellStyle).Text(item.ArrivedCount.ToString());
                }

                table.Cell().Element(CellStyle).Text("Итого").Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.InvitedCount).ToString()).Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.BookedCount).ToString()).Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.ArrivedCount).ToString()).Bold();
            });
        }

        private void BuildHoursTable(IContainer container, Models.BranchReport report)
        {
            var items = report.HoursEntries.OrderBy(x => x.Employee!.FullName).ToList();

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.5f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("ФИО администратора").Bold();
                    header.Cell().Element(CellStyle).Text("Отработанные часы").Bold();
                });

                foreach (var item in items)
                {
                    table.Cell().Element(CellStyle).Text(item.Employee!.FullName);
                    table.Cell().Element(CellStyle).Text(item.WorkedHours.ToString("0.##"));
                }

                table.Cell().Element(CellStyle).Text("Итого").Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.WorkedHours).ToString("0.##")).Bold();
            });
        }

        private void BuildReviewsTable(IContainer container, Models.BranchReport report)
        {
            var items = report.ReviewEntries.OrderBy(x => x.Employee!.FullName).ToList();

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("ФИО администратора").Bold();
                    header.Cell().Element(CellStyle).Text("Отправлено СМС").Bold();
                    header.Cell().Element(CellStyle).Text("Оставлено отзывов").Bold();
                });

                foreach (var item in items)
                {
                    table.Cell().Element(CellStyle).Text(item.Employee!.FullName);
                    table.Cell().Element(CellStyle).Text(item.SmsSentCount.ToString());
                    table.Cell().Element(CellStyle).Text(item.ReviewsLeftCount.ToString());
                }

                table.Cell().Element(CellStyle).Text("Итого").Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.SmsSentCount).ToString()).Bold();
                table.Cell().Element(CellStyle).Text(items.Sum(x => x.ReviewsLeftCount).ToString()).Bold();
            });
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten1)
                .Padding(4);
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