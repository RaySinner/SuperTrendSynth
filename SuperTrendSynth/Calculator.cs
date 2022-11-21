using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace SuperTrendSynth
{
    public class Calculator
    {
        public double A { get; set; }
        public double B { get; set; }
        public double FactorA { get; set; }
        public double FactorB { get; set; }
        public CalcFormula Formula { get; set; }
        public double Result
        {
            get
            {
                double result = 0;
                switch (Formula)
                {
                    case CalcFormula.None: result = 0; break;
                    case CalcFormula.Percent: result = Percent; break;
                    case CalcFormula.Summ: result = Summ; break;
                    case CalcFormula.Division: result = Divisor; break;
                }
                return result;
            }
        }

        private double Percent => (A * FactorA - B * FactorB) / A * FactorA * 100;
        private double Summ => A * FactorA + B * FactorB;
        private double Divisor => (A * FactorA) / (B * FactorB);

        public Calculator(CalcFormula formula, double factorA = 1, double factorB = 1)
        {
            this.Formula = formula;
            this.FactorA = factorA;
            this.FactorB = factorB;
        }
    }
}
