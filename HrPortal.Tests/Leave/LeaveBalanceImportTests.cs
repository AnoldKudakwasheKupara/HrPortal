using System.Text;
using HrPortal.Services;
using HrPortal.Services.Sanctions;

namespace HrPortal.Tests.Leave
{
    public class LeaveBalanceImportTests
    {
        private static readonly LeaveBalanceEmployee[] Employees =
        {
            new(1, "E001", "Rudo Nyandoro", "rudo@test.test"),
            new(2, "E002", "Tendai Moyo", "tendai@test.test"),
            new(3, "E003", "Tendai Moyo", "tendai2@test.test")
        };

        private static LeaveBalanceImportResult Parse(string csv) =>
            LeaveBalanceImport.Parse(
                ClientListReader.ReadTable(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "b.csv"),
                Employees);

        [Fact]
        public void Employee_numbers_and_days_are_read()
        {
            var result = Parse("Employee Number,Employee Name,Balance\ne001,Rudo Nyandoro,12.5\nE002,Tendai Moyo,\"-3,5\"\n");

            Assert.Equal(new LeaveBalanceRow(2, 1, "Rudo Nyandoro", 12.5m), result.Rows[0]);
            Assert.Equal(-3.5m, result.Rows[1].Days);
            Assert.Empty(result.Problems);
        }

        [Fact]
        public void A_blank_balance_skips_the_person()
        {
            var result = Parse("Employee Number,Balance\nE001,\nE002,4\n");

            Assert.Equal(1, result.LeftBlank);
            Assert.Equal(2, Assert.Single(result.Rows).EmployeeId);
        }

        [Fact]
        public void A_name_is_enough_unless_two_people_share_it()
        {
            var result = Parse("Name,Days\nNyandoro Rudo,4\nTendai Moyo,5\n");

            Assert.Equal(1, Assert.Single(result.Rows).EmployeeId);
            Assert.Contains("Row 3: more than one employee", Assert.Single(result.Problems));
        }

        [Fact]
        public void Rows_that_cannot_be_used_are_reported_and_the_rest_go_in()
        {
            var result = Parse("Employee Number,Balance\nE999,4\nE001,lots\nE001,5\nE001,6\nE002,63111111\n");

            Assert.Equal(5, Assert.Single(result.Rows).Days);
            Assert.Equal(4, result.Problems.Count);
            Assert.Contains(result.Problems, p => p.Contains("no employee found for \"E999\""));
            Assert.Contains(result.Problems, p => p.Contains("\"lots\" is not a number"));
            Assert.Contains(result.Problems, p => p.Contains("already in the file"));
            Assert.Contains(result.Problems, p => p.Contains("\"63111111\" is not a number"));
        }

        [Fact]
        public void A_file_without_the_right_columns_says_so()
        {
            Assert.NotNull(Parse("Staff,Salary\nE001,100\n").Error);
            Assert.NotNull(Parse("Employee Number,Salary\nE001,100\n").Error);
        }
    }
}
