using MathNet.Filtering.IIR;
using System;
using System.Globalization;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using ScottPlot.Plottables;
class Program
{
    static void Main()
    {
        string csvFilePath = @"vitor_pressao.csv";
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            // Configurações adicionais podem ser ajustadas aqui
            // Exemplo: Delimitador, se o arquivo tem cabeçalho, etc.
            HasHeaderRecord = false,
            Delimiter = ";",
            // Se o arquivo tiver cabeçalho, você pode mapear automaticamente para uma classe
            // Exemplo: AutoMap<MinhaClasse>()
        };

        List<double> signalAC = new List<double>();
        List<double> signalDC = new List<double>();

        using (var reader = new StreamReader(csvFilePath))

        using (var csv = new CsvReader(reader, config))
        {

            // Lê todos os registros do CSV como uma lista de objetos dinâmicos
            var records = csv.GetRecords<MedicoesPressao>();

            // Itera sobre os registros e faz algo com eles
            foreach (var record in records)
            {
                signalDC.Add(record.PressaoDC);
                signalAC.Add(record.PressaoAC);
            }
        }

        // Simulação de dados de entrada
        // Supondo que os dados são arrays de double para o exemplo

        var offsetInicial = 0;

        while ((signalAC[offsetInicial] > 70&& signalAC[offsetInicial]<20)|| signalDC[offsetInicial]<50)
        {
            offsetInicial++;
        }

        var offsetFinal= 0;

        while (signalDC[signalDC.Count - 1 - offsetFinal] < 30)
        {
            offsetFinal++;
        }

        signalDC = signalDC.Skip(offsetInicial).Take(signalDC.Count - offsetInicial - offsetFinal).ToList();
        signalAC = signalAC.Skip(offsetInicial).Take(signalAC.Count - offsetInicial - offsetFinal).ToList();

        // Filtragem do sinal para remover ruído e obter componentes AC e DC
        // Usando um filtro passa-baixa para obter o componente DC
        var lowPassFilter = OnlineIirFilter.CreateLowpass(MathNet.Filtering.ImpulseResponse.Finite, 1000,300, 2);
        var highPassFilter = OnlineIirFilter.CreateHighpass(MathNet.Filtering.ImpulseResponse.Finite, 1000, 3, 2);

        var filteredDC = signalDC.Select(lowPassFilter.ProcessSample).Select(highPassFilter.ProcessSample).ToList();


        // Usando um filtro passa-alta para obter o componente AC
        var filteredAC = signalAC.Select(lowPassFilter.ProcessSample).Select(highPassFilter.ProcessSample).ToList();


        var DcPeak = FindDCPeak(filteredDC.ToArray());

        filteredDC = filteredDC.Skip(DcPeak.index + 100).ToList();
        filteredAC = filteredAC.Skip(DcPeak.index + 100).ToList();
        double[] derivative = CalculateDiscreteDerivative(filteredAC.ToArray());
        double[] secondDerivative = CalculateDiscreteDerivative(derivative);




        // Detecção de picos no sinal AC para encontrar picos sistólicos
        var peaks = FindLocalMaxima(filteredAC.ToArray(), filteredDC.ToArray(), derivative, secondDerivative); // Ajuste a distância conforme necessário
        var valleys = FindValleys(filteredAC.Skip(1000).ToArray(), filteredDC.Skip(1000).ToArray(), 100); // Ajuste a distância conforme necessário



        List<double> dataX = new List<double>();

        var i = 0;
        foreach (var x in signalAC)
        {
            dataX.Add(i);
            i++;
        }

        ScottPlot.Plot myPlot1 = new();
        myPlot1.Add.Signal(signalDC);

        myPlot1.Add.Signal(signalAC);
        //myPlot1.Add.Signal(filteredDC);
        myPlot1.SavePng("SinalPuro.png", 1000, 1000);

        ScottPlot.Plot myPlot2 = new();
        myPlot2.Add.Signal(filteredDC);

        myPlot2.Add.Signal(filteredAC);
        myPlot2.Add.Signal(derivative);
        myPlot2.Add.Signal(secondDerivative);


        //myPlot2.Add.Signal(filteredAC);
        myPlot2.SavePng("SinalFiltrado.png", 1000, 1000);

        // Pressão sistólica e diastólica
        double systolicPressure = peaks.Where(x => x.valorAC >10 && x.valorDC > 0).FirstOrDefault().valorDC;

        var maxValley = valleys.OrderByDescending(x => x.valorAC).FirstOrDefault();


        double diastolicPressure = maxValley.valorDC;

        Console.WriteLine($"Pressão Sistólica: {systolicPressure}");
        Console.WriteLine($"Pressão Diastólica: {diastolicPressure}");

    }

    static PeakValleyInfo FindDCPeak(double[] signalDC)
    {
        var peak = new PeakValleyInfo()
        {
            index = -1,
            valorAC = 0,
            valorDC = 0,
        };


        for(var i = 0; i < signalDC.Length; i++)
        {
            if (signalDC[i] > peak.valorDC)
            {
                peak.valorDC = signalDC[i];
                peak.index = i; 
            }
        }

        return peak;
    }

    public static List<PeakValleyInfo> FindValleys(double[] signalAC, double[] signalDC, int minDistance)
    {
        List<PeakValleyInfo> valleys = new List<PeakValleyInfo>();

        for (int i = 1; i < signalAC.Length - 1; i++)
        {
            if (signalAC[i] < signalAC[i - 1] && signalAC[i] < signalAC[i + 1])
            {
                var valley = new PeakValleyInfo
                {
                    index = i,
                    valorAC = signalAC[i],
                    valorDC = signalDC[i],

                };
                valleys.Add(valley);
            }
        }

        return valleys;
    }

    public static List<PeakValleyInfo> FindLocalMaxima(double[] signalAC, double[] signalDC, double[] firstDerivative, double[] secondDerivative)
    {
        List<PeakValleyInfo> localMaximaIndices = new List<PeakValleyInfo>();

        for (int i = 1; i < firstDerivative.Length - 1; i++)
        {
            if (firstDerivative[i] >= 0 && firstDerivative[i + 1] < 0 && secondDerivative[i] < 0&& signalAC[i]>25)
            {
                var peak = new PeakValleyInfo
                {
                    index = i,
                    valorAC = signalAC[i],
                    valorDC = signalDC[i],

                };
                localMaximaIndices.Add(peak); // i + 1 because second derivative is one index ahead
            }
        }

        return localMaximaIndices;
    }


    public static double[] CalculateDiscreteDerivative(double[] signal)
    {
        double[] derivative = new double[signal.Length - 1];

        for (int i = 0; i < signal.Length - 1; i++)
        {
            derivative[i] = signal[i + 1] - signal[i];
        }

        return derivative;
    }


}

public class MedicoesPressao
{
    public int PressaoDC { get; set; }
    public int PressaoAC { get; set; }
}

public class PeakValleyInfo
{
    public int index { get; set; }
    public double valorAC { get; set; }
    public double valorDC { get; set; }
}