using System.Text;
using HrPortal.Services.Sanctions;

namespace HrPortal.Tests.Sanctions
{
    public class ClientListReaderTests
    {
        private static ClientTable Csv(string text) =>
            ClientListReader.ReadTable(new MemoryStream(Encoding.UTF8.GetBytes(text)), "clients.csv");

        [Fact]
        public void Common_headings_are_recognised()
        {
            var table = Csv("First Name,Middle Name,Surname,D.O.B,Nationality,ID Number,Passport No,Client Code\n");

            var map = ClientListReader.AutoMap(table.Headings);

            Assert.Equal(0, map[ClientField.FirstName]);
            Assert.Equal(1, map[ClientField.MiddleName]);
            Assert.Equal(2, map[ClientField.LastName]);
            Assert.Equal(3, map[ClientField.DateOfBirth]);
            Assert.Equal(4, map[ClientField.Nationality]);
            Assert.Equal(5, map[ClientField.IdNumber]);
            Assert.Equal(6, map[ClientField.PassportNumber]);
            Assert.Equal(7, map[ClientField.Reference]);
            Assert.True(ClientListReader.HasName(map));
        }

        [Fact]
        public void Unfamiliar_headings_need_the_columns_chosen()
        {
            var table = Csv("Customer Particulars,Identity Document\nPeter Ncube,63-123456A11\n");

            Assert.False(ClientListReader.HasName(ClientListReader.AutoMap(table.Headings)));

            var import = ClientListReader.Import(table, new Dictionary<ClientField, int>
            {
                [ClientField.FullName] = 0,
                [ClientField.IdNumber] = 1
            });

            var client = Assert.Single(import.Clients);
            Assert.Equal("Peter Ncube", client.Name);
            Assert.Equal("63-123456A11", client.IdNumber);
        }

        [Fact]
        public void Name_parts_are_put_together_and_details_read()
        {
            var table = Csv("First Name,Middle Name,Last Name,Date of Birth,Nationality,Passport Number\n" +
                            "Grace,,Chikore,31/12/1980,Zimbabwean,FN123456\n");

            var map = ClientListReader.AutoMap(table.Headings);
            var client = Assert.Single(ClientListReader.Import(table, map).Clients);

            Assert.Equal("Grace Chikore", client.Name);
            Assert.False(client.IsCompany);
            Assert.Equal(new DateTime(1980, 12, 31), client.DateOfBirth);
            Assert.Equal("Zimbabwean", client.Nationality);
            Assert.Equal("FN123456", client.PassportNumber);
            Assert.Equal(2, client.RowNumber);
        }

        [Fact]
        public void A_row_with_only_a_company_name_is_a_company()
        {
            var table = Csv("Full Name,Company Name,Registration Number\n,Moyo and Sons (Pvt) Ltd,1234/2001\n");

            var client = Assert.Single(ClientListReader.Import(table, ClientListReader.AutoMap(table.Headings)).Clients);

            Assert.Equal("Moyo and Sons (Pvt) Ltd", client.Name);
            Assert.True(client.IsCompany);
            Assert.Equal("1234/2001", client.RegistrationNumber);
        }

        [Fact]
        public void Invalid_and_duplicate_rows_are_counted_and_explained()
        {
            var table = Csv("Name,ID Number,Date of Birth\n" +
                            "Peter Ncube,63-1,1/2/1970\n" +
                            ",63-2,\n" +
                            "Peter  Ncube,63-1,01/02/1970\n" +
                            "Tendai Moyo,,31/02/1980\n");

            var import = ClientListReader.Import(table, ClientListReader.AutoMap(table.Headings));

            Assert.Equal(4, import.Received);
            Assert.Equal(2, import.Clients.Count);
            Assert.Equal(1, import.Invalid);
            Assert.Equal(1, import.Duplicates);
            Assert.Contains(import.Problems, p => p.StartsWith("Row 3: no name"));
            Assert.Contains(import.Problems, p => p.StartsWith("Row 4: the same as an earlier row"));
            Assert.Contains(import.Problems, p => p.Contains("\"31/02/1980\" could not be read"));
            Assert.Null(import.Clients.Single(c => c.Name == "Tendai Moyo").DateOfBirth);
        }

        [Theory]
        [InlineData("31/12/1980", 1980, 12, 31)]
        [InlineData("1980-12-31", 1980, 12, 31)]
        [InlineData("31 Dec 1980", 1980, 12, 31)]
        [InlineData("29586", 1980, 12, 31)]
        public void Dates_of_birth_are_read_day_first_or_iso_or_as_excel_numbers(string text, int y, int m, int d) =>
            Assert.Equal(new DateTime(y, m, d), ClientListReader.ParseDate(text));

        [Theory]
        [InlineData("12/31/1980")]
        [InlineData("sometime in 1980")]
        [InlineData("1/1/2999")]
        public void Dates_that_cannot_be_read_safely_are_not_guessed(string text) =>
            Assert.Null(ClientListReader.ParseDate(text));
    }
}
