using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using DegiroPitGenerator.Models.Configuration;
using Integrations.Degiro;
using Integrations.Degiro.Adapters;
using Integrations.Degiro.Models;
using Integrations.Degiro.Models.Configuration;
using Models.Operations;

namespace DegiroPitGenerator
{
    internal class DegiroOperationsProvider
    {
        private readonly DegiroConfiguration _configuration;
        private readonly ITransactionAdapter _transactionAdapter;
        private readonly IDividendAdapter _dividendAdapter;
        private readonly IFeeAdapter _feeAdapter;

        internal DegiroOperationsProvider()
        {
            _configuration = Configuration.GetSection<DegiroConfiguration>();
            _transactionAdapter = new TransactionAdapter(_configuration);
            _dividendAdapter = new DividendAdapter(_configuration);
            _feeAdapter = new FeeAdapter(_configuration);
        }

        internal async Task<YearOperations> GetYearOperations(int pitYear)
        {
            var localCsvConfiguration = Configuration.GetSection<DegiroCsvOverride>();

            // TWARDE WYMUSZENIE: Zawsze wczytuj pliki z dysku na podstawie ścieżek z appsettings.json.
            // Całkowicie wycięto instrukcję if/else oraz moduł IntegrationFactory.

            ICsv<CsvTransaction> transactionCsv = new Csv<CsvTransaction>(File.ReadAllText(localCsvConfiguration.TransactionsCsvPath));
            ICsv<CsvCashOperation> cashOperationCsv = new Csv<CsvCashOperation>(File.ReadAllText(localCsvConfiguration.CashOperationsCsvPath));

            // Transakcje kupna/sprzedaży wczytujemy z CAŁEJ historii (niezbędne dla poprawnego działania FIFO)
            var degiroTransactions = transactionCsv.GetRows();

            // Operacje gotówkowe (dywidendy/opłaty) filtrujemy TYLKO dla wybranego roku podatkowego
            var degiroCashOperations = cashOperationCsv.GetRows()
                .Where(x => {
                    if (DateTime.TryParse(x.Date, new CultureInfo("pl-PL"), DateTimeStyles.None, out var date))
                    {
                        return date.Year == pitYear;
                    }
                    return false; // Jeśli data jest pusta lub uszkodzona, ignorujemy wiersz
                })
                .ToList();

            return new YearOperations
            {
                Year = pitYear,
                AllTransactions = _transactionAdapter.Adapt(degiroTransactions),
                YearDividends = _dividendAdapter.Adapt(degiroCashOperations),
                YearFees = _feeAdapter.Adapt(degiroCashOperations)
            };
        }
    }
}