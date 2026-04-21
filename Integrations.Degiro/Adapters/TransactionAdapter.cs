using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Extensions;
using Integrations.Degiro.Models;
using Integrations.Degiro.Models.Configuration;
using Models;
using Models.Operations;
using Models.Operations.Transactions;

namespace Integrations.Degiro.Adapters
{
    public interface ITransactionAdapter
    {
        IEnumerable<Transaction> Adapt(List<CsvTransaction> degiroTransactions);
    }

    public class TransactionAdapter : ITransactionAdapter
    {
        private readonly DegiroConfiguration _configuration;
        private readonly CultureInfo _datesCultureInfo;

        public TransactionAdapter(DegiroConfiguration configuration)
        {
            _configuration = configuration;
            _datesCultureInfo = CultureInfo.GetCultureInfo(configuration.Domain.ReportsIsoLanguageCode);
        }

        public IEnumerable<Transaction> Adapt(List<CsvTransaction> degiroTransactions)
        {
            // 1. Rozdzielamy plik: wiersze z ID to prawdziwe transakcje, bez ID to operacje kapitałowe (splity)
            var normalTransactions = degiroTransactions.Where(t => !string.IsNullOrEmpty(t.TransactionId)).ToList();
            var corporateActions = degiroTransactions.Where(t => string.IsNullOrEmpty(t.TransactionId)).ToList();

            // 2. Szukamy splitów grupując operacje po dacie i kodzie ISIN
            var splitGroups = corporateActions.GroupBy(t => new { t.Date, t.Isin });

            foreach (var group in splitGroups)
            {
                // Zliczamy akcje zabrane (-) i akcje przyznane (+)
                var oldShares = group.Where(t => (t.Quantity ?? 0) < 0).Sum(t => t.Quantity ?? 0);
                var newShares = group.Where(t => (t.Quantity ?? 0) > 0).Sum(t => t.Quantity ?? 0);

                // Jeśli broker zabrał akcje i dał nowe tego samego dnia dla tego samego ISIN - mamy SPLIT
                if (oldShares < 0 && newShares > 0)
                {
                    // Obliczamy proporcję, np. dał 40, zabrał 10 -> Mnożnik = 4
                    // W przypadku reverse-splitu (np. zabrał 10, dał 1) -> Mnożnik = 0.1

                    // Dodajemy (decimal) przed dzieleniem, żeby program wyliczył dokładny ułamek (np. 1.5), a nie uciął do 1
                    decimal multiplier = Math.Abs((decimal)newShares / oldShares);

                    // 3. Aplikujemy wyliczony mnożnik do wszystkich wcześniejszych zakupów tych akcji
                    foreach (var transaction in normalTransactions)
                    {
                        if (transaction.Isin == group.Key.Isin &&
                            DateTime.Parse(transaction.Date, _datesCultureInfo) < DateTime.Parse(group.Key.Date, _datesCultureInfo))
                        {
                            // Dodajemy jawne rzutowanie (int?)
                            transaction.Quantity = (int?)(transaction.Quantity * multiplier);
                        }
                    }
                }
            }

            // 4. BARDZO WAŻNE: Od teraz program do wyliczania podatków używa TYLKO normalnych transakcji!
            // Pozbyliśmy się operacji kapitałowych, więc nie wygenerują one fałszywych podatków.
            degiroTransactions = normalTransactions;

            foreach (var degiroTransaction in degiroTransactions.OrderBy(_ => DateTime.Parse(_.Date, _datesCultureInfo)).ThenBy(_ => TimeSpan.Parse(_.Time)).GroupBy(_ => _.TransactionId))
            {
                var feeSum = degiroTransaction.Sum(_ => Math.Abs(_.FeeAmount ?? 0));

                //The Distinct().Single() confirms that all dates are the same within TransactionId
                yield return new Transaction
                {
                    Id = degiroTransaction.SelectSingle(_ => _.TransactionId),
                    FinancialInstrumentCommonName = degiroTransaction.SelectSingle(_ => _.InstrumentName),
                    FinancialInstrumentReference = degiroTransaction.SelectSingle(_ => $"{_.Isin}.{_.StockExchangeName}"),
                    Date = Convert.ToDateTime(degiroTransaction.SelectSingle(_ => _.Date), _datesCultureInfo),
                    TransactionType = degiroTransaction
                        .SelectSingle(_ => _.Quantity > 0 ? TransactionType.BUY : TransactionType.SELL),
                    Quantity = degiroTransaction.Sum(_ => _.Quantity.Value),
                    TransactionPrice = Math.Abs(degiroTransaction.Sum(_ => _.LocalValue.Value)),
                    TransactionCurrency = degiroTransaction.SelectSingle(_ => Enum.Parse<Currency>(_.LocalCurrency)),
                    Fee = feeSum,
                    //Sometimes there are no transaction Fees at all. Then FeeCurrency is left blank
                    FeeCurrency = feeSum != 0
                        ? degiroTransaction.Where(_ => _.FeeCurrency != "")
                            .SelectSingle(_ => Enum.Parse<Currency>(_.FeeCurrency))
                        : default,
                    StockExchangeCountry = SelectCountry(degiroTransaction.SelectSingle(_ => _.StockExchangeName))
                };
            }
        }

        private Country SelectCountry(string stockExchangeName)
        {
            return _configuration.Domain.StockCountriesMapping[stockExchangeName];
        }
    }
}
