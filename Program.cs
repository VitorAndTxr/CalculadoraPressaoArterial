using MathNet.Filtering.IIR;
using System;
using System.Globalization;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
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
                //Console.WriteLine($"PressaoAC: {record.PressaoAC}, PressaoDC: {record.PressaoDC}");
                // Aqui você pode processar cada registro conforme necessário

                var tensaoDC = (record.PressaoDC*3.3)/4095;
                var tensaoAC = (record.PressaoAC * 3.3)/4095;

                //Console.WriteLine($"tensaoAC: {tensaoAC}, tensaoDC: {tensaoDC}");

                var PmmHgDC = (59.136 * (tensaoDC + 60)) + 3.2598;
                var PmmHgAC = (59.136 * (tensaoAC + 60)) + 3.2598;

                //Console.WriteLine($"PmmHgDC: {tensaoAC}, PmmHgAC: {tensaoDC}");


                signalAC.Add(PmmHgAC);
                signalDC.Add(PmmHgDC);
            }
        }

        // Simulação de dados de entrada
        // Supondo que os dados são arrays de double para o exemplo


        // Filtragem do sinal para remover ruído e obter componentes AC e DC
        // Usando um filtro passa-baixa para obter o componente DC
        var lowPassFilter = OnlineIirFilter.CreateLowpass(MathNet.Filtering.ImpulseResponse.Finite,100,40, 2);
        double[] filteredDC = signalDC.Skip(1000).Select(lowPassFilter.ProcessSample).ToArray();

        // Usando um filtro passa-alta para obter o componente AC
        var highPassFilter = OnlineIirFilter.CreateHighpass(MathNet.Filtering.ImpulseResponse.Finite, 100, 5, 2);
        double[] filteredAC = signalAC.Skip(1000).Select(highPassFilter.ProcessSample).ToArray();

        // Detecção de picos no sinal AC para encontrar picos sistólicos
        var peaks = FindPeaks(filteredAC, 10); // Ajuste a distância conforme necessário


        List<double> dataX = new List<double>();

        var i = 0;
        foreach (var x in signalAC)
        {
            dataX.Add(i);
            i++;
        }

        ScottPlot.Plot myPlot1 = new();
        myPlot1.Add.Signal(signalDC);

        //myPlot1.Add.Signal(filteredDC);
        myPlot1.SavePng("PmmHgDC.png", 1000, 1000);

        ScottPlot.Plot myPlot2 = new();
        myPlot2.Add.Signal(signalAC);

        //myPlot2.Add.Signal(filteredAC);
        myPlot2.SavePng("PmmHgAC.png", 1000, 1000);

        // Pressão sistólica e diastólica
        double systolicPressure = peaks.Max();
        double diastolicPressure = peaks.Min();

        Console.WriteLine($"Pressão Sistólica: {systolicPressure}");
        Console.WriteLine($"Pressão Diastólica: {diastolicPressure}");



    }

    static double[] FindPeaks(double[] signal, int minDistance)
    {
        var peaks = new System.Collections.Generic.List<double>();

        for (int i = 1; i < signal.Length - 1; i++)
        {
            if (signal[i] > signal[i - 1] && signal[i] > signal[i + 1])
            {
                if (peaks.Count == 0 || i - peaks.Last() >= minDistance)
                {
                    peaks.Add(signal[i]);
                }
            }
        }

        return peaks.ToArray();
    }


}

public class MedicoesPressao
{
    public int PressaoDC { get; set; }
    public int PressaoAC { get; set; }
}