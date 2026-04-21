using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Extensions;
using Integrations.Degiro.Models;

namespace Integrations.Degiro
{
    public interface ICsv<T>
    {
        List<T> GetRows();
    }

    public class Csv<T> : ICsv<T>
    {
        private readonly List<T> _records;

        public Csv(string csv)
        {
            using var stringReader = new StringReader(csv);

            using var csvReader = new CsvReader(stringReader, new CsvConfiguration(new CultureInfo("pl-PL"))
            {
                //Depending on language version headers may be named differently.
                //Therefore the most reasonable way is to just base on columns order.
                HeaderValidated = null,
                MissingFieldFound = null,
                Delimiter = ","      // Twardo wymusza przecinek jako oddzielacz kolum
            });
            _records = Fix(csvReader.GetRecords<T>().ToList());
        }
        public List<T> GetRows()
        {
            return _records;
        }
        private List<T> Fix(List<T> records)
        {
            if (records is List<CsvTransaction> transactions)
            {
                // Usuwamy tylko te wiersze, które nie mają ANI ID, ANI ilości akcji
                // transactions.RemoveAll(_ => string.IsNullOrEmpty(_.TransactionId));
            }
            else if (records is List<CsvCashOperation> cashOperations)
            {
                // NOWA LINIJKA: Usuń wszystkie operacje dla polskich papierów (ISIN zaczyna się od PL)
                cashOperations.RemoveAll(_ => !string.IsNullOrEmpty(_.Isin) && _.Isin.StartsWith("PL"));
                // USUNIĘTO dzielenie przez 100. 
                // Dzięki 'pl-PL' CsvReader natywnie rozumie przecinki i pobiera poprawne kwoty.
            }
            return records;
        }
    }
}
