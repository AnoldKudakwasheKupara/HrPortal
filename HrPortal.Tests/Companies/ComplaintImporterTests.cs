using System.Text;
using HrPortal.Entities;
using HrPortal.Services;

namespace HrPortal.Tests.Companies
{
    public class ComplaintImporterTests
    {
        private const string Register =
            ",,,,,,,,,\n" +
            ",,,,,,,,,\n" +
            "Date Received,Client Name,Phone Number,Email adress,Nature of the Complaint,Staff member involved,Action Taken,Resolution Date,Status,Remarks\n" +
            "3 February 2025,Anna Test ,771000001,anna@example.com,Funds slow to arrive,Sam Staff,Called the bank,3 February 2025,Resolved,Bank was slow\n" +
            "6-10-2026,Ben Test,771000002,ben@example.com,Dividend not paid,Sam Staff,Contacted the transfer secretaries,13 October 2026,Resolved,\n" +
            "5/11/2026,Cara Test,447000000003,cara@example.com,,,,,Pending,\n" +
            ",Dan Test,,dan@example.com   Did not get the valuation on time,,,,,,\n" +
            "7/2/2026,Eve Test,,eve@example.com,Deal note late,Sam Staff,Deal note sent,,Resolved,\n" +
            "1/1/2026,,,nobody@example.com,No name given,,,,,\n" +
            "31/31/2026,Finn Test,,,Something,,,,,\n";

        private static ComplaintImport Read(string csv) =>
            ComplaintImporter.Read(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "register.csv");

        [Fact]
        public void A_register_with_blank_rows_above_the_headings_is_read()
        {
            var read = Read(Register);

            Assert.Equal(
                new[] { "Anna Test", "Ben Test", "Cara Test", "Dan Test", "Eve Test", "Finn Test" },
                read.Complaints.Select(c => c.ClientName));

            var anna = read.Complaints[0];

            Assert.Equal(new DateTime(2025, 2, 3), anna.ReceivedOn);
            Assert.Equal("771000001", anna.ClientPhone);
            Assert.Equal("anna@example.com", anna.ClientEmail);
            Assert.Equal("Funds slow to arrive", anna.Nature);
            Assert.Equal("Sam Staff", anna.StaffInvolved);
            Assert.Equal(ComplaintStatus.Resolved, anna.Status);
            Assert.Equal(new DateTime(2025, 2, 3), anna.ResolvedOn);
            Assert.Equal("Bank was slow", anna.Remarks);
        }

        [Fact]
        public void Dates_are_read_day_first_however_they_were_typed()
        {
            var read = Read(Register);

            Assert.Equal(new DateTime(2026, 10, 6), read.Complaints[1].ReceivedOn);
            Assert.Equal(new DateTime(2026, 11, 5), read.Complaints[2].ReceivedOn);
            Assert.Null(read.Complaints[3].ReceivedOn);
            Assert.Null(read.Complaints[5].ReceivedOn);
            Assert.Contains(read.Problems, p => p.Contains("Finn Test") && p.Contains("31/31/2026"));
        }

        [Fact]
        public void A_complaint_typed_into_the_email_column_goes_with_the_complaint()
        {
            var dan = Read(Register).Complaints[3];

            Assert.Equal("dan@example.com", dan.ClientEmail);
            Assert.Equal("Did not get the valuation on time", dan.Nature);
            Assert.Equal(ComplaintStatus.Pending, dan.Status);
        }

        [Fact]
        public void Resolved_without_a_date_stays_resolved_and_blank_is_pending()
        {
            var read = Read(Register);

            Assert.Equal(ComplaintStatus.Resolved, read.Complaints[4].Status);
            Assert.Null(read.Complaints[4].ResolvedOn);
            Assert.Equal(ComplaintStatus.Pending, read.Complaints[2].Status);
        }

        [Fact]
        public void A_row_without_a_client_name_is_left_out_and_reported()
        {
            var read = Read(Register);

            Assert.Equal(7, read.Received);
            Assert.Contains(read.Problems, p => p.Contains("no client name"));
        }

        [Fact]
        public void The_same_complaint_loaded_twice_is_recognised()
        {
            var first = Read(Register).Complaints;
            var again = Read(Register).Complaints;

            Assert.Equal(
                first.Select(ComplaintImporter.Key),
                again.Select(ComplaintImporter.Key));

            Assert.Equal(first.Count, first.Select(ComplaintImporter.Key).Distinct().Count());
        }

        [Fact]
        public void A_sheet_without_a_client_name_heading_is_refused_plainly()
        {
            var read = Read("Name of thing,Other\nx,y\n");

            Assert.Empty(read.Complaints);
            Assert.Contains("Client Name", Assert.Single(read.Problems));
        }
    }
}
