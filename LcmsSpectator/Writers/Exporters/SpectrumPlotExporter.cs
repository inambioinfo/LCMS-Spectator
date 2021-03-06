﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LcmsSpectator.Writers.Exporters
{
    using System.IO;

    using InformedProteomics.Backend.Data.Spectrometry;
    using InformedProteomics.Backend.Utils;

    using LcmsSpectator.Models;
    using LcmsSpectator.PlotModels;
    using LcmsSpectator.PlotModels.ColorDicionaries;
    using LcmsSpectator.Utils;

    using OxyPlot;
    using OxyPlot.Annotations;
    using OxyPlot.Axes;
    using OxyPlot.Series;

    public class SpectrumPlotExporter
    {
        private readonly IEnumerable<IonType> ionTypes;

        private readonly string outputFile;

        private readonly HashSet<BaseIonType> baseIonTypes;

        private readonly int dpi;

        public SpectrumPlotExporter(string outputFile, IEnumerable<BaseIonType> baseIonTypes, int dpi)
        {
            this.outputFile = outputFile;
            baseIonTypes = baseIonTypes ?? new HashSet<BaseIonType> { BaseIonType.B, BaseIonType.Y };
            this.baseIonTypes = new HashSet<BaseIonType>();
            this.baseIonTypes.UnionWith(baseIonTypes);

            var ionTypeFactory = new IonTypeFactory(100);
            var allIonTypes = ionTypeFactory.GetAllKnownIonTypes();
            this.ionTypes = allIonTypes.Where(ionType => this.baseIonTypes.Contains(ionType.BaseIonType));
            this.dpi = dpi;
        }

        public async Task ExportAsync(IList<PrSm> ids, IProgress<ProgressData> progress = null)
        {
            progress = progress ?? new Progress<ProgressData>();
            var progressData = new ProgressData();
            int i = 1;
            foreach (var id in ids)
            {
                var outPath = Path.Combine(this.outputFile, string.Format("Scan_{0}.png", id.Scan));
                await this.ExportAsync(id, outPath);
                progress.Report(progressData.UpdatePercent((100.0 * i++) / ids.Count));
            }
        }

        public void Export(IList<PrSm> ids, IProgress<ProgressData> progress = null)
        {
            progress = progress ?? new Progress<ProgressData>();
            var progressData = new ProgressData();
            int i = 1;
            foreach (var id in ids)
            {
                var outPath = Path.Combine(this.outputFile, string.Format("Scan_{0}.png", id.Scan));
                this.Export(id, outPath);
                progress.Report(progressData.UpdatePercent((100.0 * i++) / ids.Count));
            }
        }

        public async Task ExportAsync(PrSm id, string outputPath)
        {
            var plotModel = await this.GetPlotModelAsync(id);
            DynamicResolutionPngExporter.Export(
                        plotModel,
                        outputPath,
                        1280,
                        1024,
                        OxyColors.White,
                        this.dpi);
        }

        public void Export(PrSm id, string outputPath)
        {
            var plotModel = this.GetPlotModel(id);
            DynamicResolutionPngExporter.Export(
                plotModel,
                outputPath,
                1280,
                1024,
                OxyColors.White,
                this.dpi);
        }

        private Task<PlotModel> GetPlotModelAsync(PrSm id)
        {
            return Task.Run(() => this.GetPlotModel(id));
        }

        private PlotModel GetPlotModel(PrSm id)
        {
            var lcms = id.LcMs;
            var fragSequence = id.GetFragmentationSequence();
            var msLevel = lcms.GetMsLevel(id.Scan);
            var fragments = msLevel == 2 ?
                            fragSequence.GetFragmentLabels(this.ionTypes.Where(ionType => ionType.Charge <= id.Charge).ToList()) :
                            fragSequence.GetChargePrecursorLabels();
            var spectrum = lcms.GetSpectrum(id.Scan, true);

            // Set up plot
            var plotModel = new PlotModel
            {
                Title = msLevel == 2 ? string.Format("MS/MS Scan {0}", id.Scan) :
                                       string.Format("MS Scan {0}", id.Scan),
                IsLegendVisible = false,

            };

            // Add axes
            var xaxis = new LinearAxis { Title = "M/Z", Position = AxisPosition.Bottom };
            var yaxis = new LinearAxis { Title = "Intensity", Position = AxisPosition.Left, Minimum = 0 };
            plotModel.Axes.Add(xaxis);
            plotModel.Axes.Add(yaxis);

            // Add spectrum peaks
            var spectrumPeaks = spectrum.Peaks.Select(peak => new PeakDataPoint(peak.Mz, peak.Intensity, 0.0, 0.0, string.Empty));
            var spectrumStemSeries = new StemSeries
            {
                ItemsSource = spectrumPeaks,
                Color = OxyColors.Black,
                StrokeThickness = 0.5,
            };
            plotModel.Series.Add(spectrumStemSeries);

            // Add ion highlights
            var colors = new IonColorDictionary(id.Charge);
            foreach (var fragment in fragments)
            {
                var points = fragment.GetPeaks(spectrum, false, false);
                if (points.Count == 0 || points[0].Error.Equals(double.NaN))
                {
                    continue;
                }

                var firstPoint = points[0];
                var color = firstPoint.IonType != null ? colors.GetColor(firstPoint.IonType.BaseIonType, firstPoint.IonType.Charge)
                                                           : colors.GetColor(firstPoint.Index);
                var ionSeries = new PeakPointSeries
                {
                    Color = color,
                    StrokeThickness = 1.5,
                    ItemsSource = points,
                    Title = points[0].Title,
                };

                // Create ion name annotation
                var annotation = new TextAnnotation
                {
                    Text = points[0].Title,
                    TextColor = color,
                    FontWeight = FontWeights.Bold,
                    Layer = AnnotationLayer.AboveSeries,
                    FontSize = 12,
                    Background = OxyColors.White,
                    Padding = new OxyThickness(0.1),
                    TextPosition = new DataPoint(points[0].X, points[0].Y),
                    StrokeThickness = 0
                };
                plotModel.Series.Add(ionSeries);
                plotModel.Annotations.Add(annotation);
            }

            return plotModel;
        }
    }
}
