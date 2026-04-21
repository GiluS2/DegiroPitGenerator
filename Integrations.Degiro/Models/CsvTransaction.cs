using CsvHelper.Configuration.Attributes;

namespace Integrations.Degiro.Models
{
    public class CsvTransaction
    {
        [Index(0)]
        public string Date { get; set; }

        [Index(1)]
        public string Time { get; set; }

        [Index(2)]
        public string InstrumentName { get; set; }

        [Index(3)]
        public string Isin { get; set; }

        [Index(4)]
        public string StockExchangeName { get; set; }

        [Index(5)]
        public string StockLocation { get; set; }

        [Index(6)]
        public int? Quantity { get; set; }

        [Index(7)]
        public decimal? UnitPrice { get; set; }

        [Index(8)]
        public string UnitPriceCurrency { get; set; }

        [Ignore]
        public string DegiroCurrency { get; set; } = "EUR";

        [Ignore]
        public string TotalAmountCurrency { get; set; } = "EUR";

        [Ignore]
        public string FeeCurrency { get; set; } = "EUR";

        [Index(9)]
        public decimal? LocalValue { get; set; }

        [Index(10)]
        public string LocalCurrency { get; set; }

        [Index(11)]
        public decimal? DegiroAmount { get; set; }

        [Index(12)]
        public decimal? ExchangeRate { get; set; }

        [Index(13)]
        public decimal? AutoFxFee { get; set; }

        [Index(14)]
        public decimal? TransactionFee { get; set; }

        [Ignore]
        public decimal? FeeAmount
        {
            get
            {
                // Jeśli w danym wierszu w ogóle nie ma opłat, zwracamy brak wartości
                if (TransactionFee == null && AutoFxFee == null) return null;

                // Zwracamy sumę obu kolumn (traktując pustą komórkę jako 0)
                return (TransactionFee ?? 0) + (AutoFxFee ?? 0);
            }
            set
            {
                // Pusty setter
            }
        }

        [Index(15)]
        public decimal? TotalAmount { get; set; }

        [Index(16)]
        public string TransactionIdCol16 { get; set; }

        [Index(17)]
        public string TransactionIdCol17 { get; set; }

        [Ignore]
        public string TransactionId
        {
            get
            {
                // Sprawdzamy, czy kolumna 16 ma jakiś tekst. Jeśli tak, bierzemy ją.
                if (!string.IsNullOrWhiteSpace(TransactionIdCol16))
                {
                    return TransactionIdCol16.Trim();
                }

                // Jeśli 16 jest pusta, bierzemy 17 (lub pusty tekst, jeśli obie są puste)
                if (!string.IsNullOrWhiteSpace(TransactionIdCol17))
                {
                    return TransactionIdCol17.Trim();
                }

                return "";
            }
            set
            {
                // Pusty setter
            }
        }
    }
}
