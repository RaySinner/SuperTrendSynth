using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace SuperTrendSynth
{
	public class SuperTrendSynth : Indicator, IWatchlistIndicator
    {
        #region Input params
        [InputParameter("A Symbol", 10)]
        public Symbol aSymbol;

        [InputParameter("B Symbol", 20)]
        public Symbol bSymbol;

        [InputParameter("Synth formula", 30, variants: new object[] {
             "Devision 'A/B'", CalcFormula.Division,
             "Summ 'A+B'", CalcFormula.Summ,
             "Percent '(A-B)/A*100'", CalcFormula.Division,
        })]
        public CalcFormula synthFormula = CalcFormula.Summ;

        [InputParameter("Factor A symbol", 40, 0.1, 50, 0.1, 1)]
        public double factorA = 1;

        [InputParameter("Factor B symbol", 50, 0.1, 50, 0.1, 1)]
        public double factorB = 1;

        [InputParameter("Sources price", 60, variants: new object[] {
             "Close", PriceType.Close,
             "Open", PriceType.Open,
             "High", PriceType.High,
             "Low", PriceType.Low,
             "HL2", PriceType.Median,
             "HLC3", PriceType.Typical,
             "OHLC4", PriceType.Weighted
        })]
        public PriceType sourcePrice = PriceType.Median;

        [InputParameter("Period ATR", 70, 1, 1000, 1, 0)]
        public int atrPeriod = 14;

        [InputParameter("Factor ATR", 80, 0.1, 50, 0.1, 1)]
        public double atrFactor = 3;
        #endregion Input params

        private HistoricalData aHistory;
        private HistoricalData bHistory;

        private LineSeries sourceSeries;
        private LineSeries supertrendSeries;

        private SeriesHolder indexOpenBuffer;
        private SeriesHolder indexHighBuffer;
        private SeriesHolder indexLowBuffer;
        private SeriesHolder indexCloseBuffer;
        private SeriesHolder indexTrueRangeBuffer;
        private SeriesHolder indexAtrBuffer;
        private SeriesHolder upLineBuffer;
        private SeriesHolder downLineBuffer;

        private Calculator calculator;

        public int MinHistoryDepths => this.atrPeriod + 2;
        public override string ShortName
        {
            get
            {
                if (aSymbol is null && bSymbol is null)
                    return Name + " Symbols not set";

                if (aSymbol is null && bSymbol is not null)
                    return Name + " A symbol not set";

                if (aSymbol is not null && bSymbol is null)
                    return Name + " B symbol not set";

                switch (synthFormula)
                {
                    case CalcFormula.Division: return Name + " Formula: " + $@"{factorA} * {aSymbol.Name}[{Enum.GetName(typeof(PriceType), sourcePrice)}] / {factorB} * {bSymbol.Name}[{Enum.GetName(typeof(PriceType), sourcePrice)}]";
                    case CalcFormula.Percent: return Name + " Formula: " + $@"({factorA} * {aSymbol.Name}[{Enum.GetName(typeof(PriceType), sourcePrice)}] - {factorB} * {bSymbol.Name}[{Enum.GetName(typeof(PriceType), sourcePrice)}]) / {factorA} * {aSymbol.Name}[{Enum.GetName(typeof(PriceType), sourcePrice)}] * 100";
                    case CalcFormula.Summ: return Name + " Formula: " + $@"{factorA} * {aSymbol.Name}[{Enum.GetName(typeof(PriceType), sourcePrice)}] + {factorB} * {bSymbol.Name}[{Enum.GetName(typeof(PriceType), sourcePrice)}]";
                    default: return Name + " Formula: not set";
                }
            }
        }

        public SuperTrendSynth() : base()
        {
            Name = "SuperTrendSynth";
            Description = "Synthetic supertrend indicator";

            sourceSeries         = AddLineSeries("Source", Color.AliceBlue, 1, LineStyle.Solid);
            supertrendSeries     = AddLineSeries("Supertrend", Color.AliceBlue, 1, LineStyle.Solid);

            SeparateWindow = true;
        }

        protected override void OnInit()
        {
            upLineBuffer = new SeriesHolder();
            downLineBuffer = new SeriesHolder();
            indexOpenBuffer = new SeriesHolder();
            indexHighBuffer = new SeriesHolder();
            indexLowBuffer = new SeriesHolder();
            indexCloseBuffer = new SeriesHolder();
            indexTrueRangeBuffer = new SeriesHolder();
            indexAtrBuffer = new SeriesHolder();

            calculator = new Calculator(synthFormula);

            if (aSymbol is null || bSymbol is null)
                return;

            aHistory = aSymbol.GetHistory(HistoricalData.Period, HistoricalData.HistoryType, HistoricalData.FromTime);
            bHistory = bSymbol.GetHistory(HistoricalData.Period, HistoricalData.HistoryType, HistoricalData.FromTime);

            aHistory.HistoryItemUpdated += HistoryItemUpdated;
            bHistory.HistoryItemUpdated += HistoryItemUpdated;

            aHistory.NewHistoryItem += HistoryItemUpdated;
            bHistory.NewHistoryItem += HistoryItemUpdated;
        }

        private void HistoryItemUpdated(object sender, HistoryEventArgs e)
        {
            MainLogic();
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            upLineBuffer.UpdateCount(Count);
            downLineBuffer.UpdateCount(Count);
            indexOpenBuffer.UpdateCount(Count);
            indexHighBuffer.UpdateCount(Count);
            indexLowBuffer.UpdateCount(Count);
            indexCloseBuffer.UpdateCount(Count);
            indexTrueRangeBuffer.UpdateCount(Count);
            indexAtrBuffer.UpdateCount(Count);

            MainLogic();
        }

        private void MainLogic()
        {
            if (NeedToSkipUpdate())
                return;

            UpdateSourceSeries(sourcePrice, offset: Count - 1);

            if (Count <= atrPeriod)
                return;

            UpdateAtrSeries();

            if (Count + 1 <= atrPeriod)
                return;

            UpdateSuperTrendSeries();
        }

        private bool NeedToSkipUpdate()
        {
            if (aHistory is null || bHistory is null)
                return true;

            if (aHistory.Count != bHistory.Count)
                return true;

            if (Count == 0)
                return true;

            return false;
        }

        private void UpdateSourceSeries(PriceType priceType, int offset)
        {
            sourceSeries.SetValue(GetIndexValue(priceType, offset));

            indexOpenBuffer.SetValue(GetIndexValue(PriceType.Open, offset));
            indexHighBuffer.SetValue(new List<double> { GetIndexValue(PriceType.Low, offset), GetIndexValue(PriceType.High, offset) }.Max());
            indexLowBuffer.SetValue(new List<double> { GetIndexValue(PriceType.Low, offset), GetIndexValue(PriceType.High, offset) }.Min());
            indexCloseBuffer.SetValue(GetIndexValue(PriceType.Close, offset));

            double tr = new List<double> 
            { 
                indexHighBuffer.GetValue() - indexLowBuffer.GetValue(),
                Math.Abs(indexHighBuffer.GetValue() - indexCloseBuffer.GetValue(1)),
                Math.Abs(indexLowBuffer.GetValue() - indexCloseBuffer.GetValue(1))
            }.Max();

            indexTrueRangeBuffer.SetValue(tr);
        }

        private void UpdateAtrSeries()
        {
            List<double> trSeries = new List<double>();

            for (int i = 0; i < atrPeriod; i++)
            {
                trSeries.Add(indexTrueRangeBuffer.GetValue(i));
            }

            double atr = trSeries.Sum() / trSeries.Count;

            indexAtrBuffer.SetValue(atr);
        }

        private void UpdateSuperTrendSeries()
        {
            double currentSourceValue = sourceSeries.GetValue();
            double currentIndexAtrValue = indexAtrBuffer.GetValue();
            double currentIndexCloseValue = indexCloseBuffer.GetValue();
            double prevIndexCloseValue = indexCloseBuffer.GetValue(1);

            double upValue = currentSourceValue + atrFactor * currentIndexAtrValue;
            double downValue = currentSourceValue - atrFactor * currentIndexAtrValue;

            double prevUpValue = 0;
            double prevDownValue = 0;
            int direction = 1;
            double prevSupertrendValue = 0;

            if (Count > atrPeriod + 2)
            {
                prevUpValue = upLineBuffer.GetValue(1);
                prevDownValue = downLineBuffer.GetValue(1);
                prevSupertrendValue = supertrendSeries.GetValue(1);

                upValue = upValue < prevUpValue || prevIndexCloseValue > prevUpValue ? upValue : prevUpValue;
                downValue = downValue > prevDownValue || prevIndexCloseValue < prevDownValue ? downValue : prevDownValue;

                if (prevSupertrendValue == prevUpValue)
                    direction = currentIndexCloseValue > upValue ? -1 : 1;
                else
                    direction = currentIndexCloseValue < downValue ? 1 : -1;
            }

            upLineBuffer.SetValue(upValue);
            downLineBuffer.SetValue(downValue);

            supertrendSeries.SetValue( direction == -1 ? downValue : upValue);

            if (direction == 1)
                supertrendSeries.SetMarker(0, Color.Red);
            else
                supertrendSeries.SetMarker(0, Color.Green);
        }

        private double GetPriceByHistory(HistoricalData historicalData, PriceType priceType, int offset = 0)
        {
            if (offset == historicalData.Count)
                offset--;

            return historicalData[offset, SeekOriginHistory.Begin][priceType];
        }

        private double GetIndexValue(PriceType priceType, int offset)
        {
            var aDeterm = GetPriceByHistory(aHistory, priceType, offset);
            var bDeterm = GetPriceByHistory(bHistory, priceType, offset);

            calculator.A = aDeterm;
            calculator.B = bDeterm;

            return calculator.Result;
        }
    }
}
