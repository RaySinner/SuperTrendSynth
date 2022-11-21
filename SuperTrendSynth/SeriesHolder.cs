using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperTrendSynth
{
    public class SeriesHolder
    {
        private List<double> series;
        private object locker = new object();

        public SeriesHolder()
        {
            series = new List<double>();
        }

        public void UpdateCount(int count)
        {
            lock (locker)
            {
                if (count > series.Count)
                    while (count > series.Count)
                    {
                        if (series.Count == 0)
                            series.Add(double.NaN);
                        else
                            series.Add(series.Last());
                    }

                if (count < series.Count)
                    while (count < series.Count)
                        series.Remove(series.Last());
            }
        }

        public double GetValue(int offset = 0)
        {
            lock (locker)
            {
                if (series.Count <= offset)
                    return double.NaN;

                return series[series.Count - 1 - offset];
            }
        }

        public void SetValue(double value, int offset = 0)
        {
            lock (locker)
            {
                if (series.Count <= offset)
                    return;

                series[series.Count - 1 - offset] = value;
            }
        }
    }
}
