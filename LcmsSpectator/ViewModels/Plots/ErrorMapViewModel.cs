﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ErrorMapViewModel.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   This class maintains a heat map plot model showing sequence vs ion type vs error.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.ViewModels.Plots
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using InformedProteomics.Backend.Data.Sequence;
    using InformedProteomics.Backend.Data.Spectrometry;

    using LcmsSpectator.Config;
    using LcmsSpectator.DialogServices;
    using LcmsSpectator.PlotModels;
    using LcmsSpectator.Utils;
    using LcmsSpectator.Writers.Exporters;

    using OxyPlot;
    using OxyPlot.Axes;
    using OxyPlot.Series;

    using ReactiveUI;

    /// <summary>
    /// This class maintains a heat map plot model showing sequence vs ion type vs error.
    /// </summary>
    public class ErrorMapViewModel : ReactiveObject
    {
        /// <summary>
        /// Dialog service for opening dialogs from view model.
        /// </summary>
        private readonly IDialogService dialogService;

        /// <summary>
        /// Horizontal axis of the error map plot (ion types)
        /// </summary>
        private readonly LinearAxis xaxis;

        /// <summary>
        /// Vertical axis of error map plot (sequence residue)
        /// </summary>
        private readonly LinearAxis yaxis;

        /// <summary>
        /// Color axis of the error map plot (peak error)
        /// </summary>
        private readonly LinearColorAxis colorAxis;

        /// <summary>
        /// Ion types to be displayed on y axis.
        /// </summary>
        private IonType[] ionTypes;

        /// <summary>
        /// The data that is shown in the "Table" view. This excludes any fragments without data.
        /// </summary>
        private ReactiveList<PeakDataPoint> dataTable;

        /// <summary>
        /// The iontype selected from the error map.
        /// </summary>
        private string selectedIonType;

        /// <summary>
        /// The residue and index of the seleted item.
        /// </summary>
        private string selectedAminoAcid;

        /// <summary>
        /// The value of the selected item.
        /// </summary>
        private string selectedValue;

        /// <summary>
        /// The currently selected sequence.
        /// </summary>
        private Sequence selectedSequence;

        /// <summary>
        /// The currently selected peak data points.
        /// </summary>
        private IEnumerable<IList<PeakDataPoint>> selectedPeakDataPoints;

        /// <summary>
        /// A value indicating whether multiple charge states for an ion should
        /// be combined into a single row on the heat map.
        /// </summary>
        private bool shouldCombineChargeStates;

        /// <summary>
        /// Initializes a new instance of the ErrorMapViewModel class. 
        /// </summary>
        /// <param name="dialogService">
        /// The dialog Service.
        /// </param>
        public ErrorMapViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;
            this.PlotModel = new PlotModel { Title = "Error Map", PlotAreaBackground = OxyColors.Navy };
            this.selectedPeakDataPoints = new IList<PeakDataPoint>[0];
            this.ShouldCombineChargeStates = true;

            // Init x axis
            this.xaxis = new LinearAxis
            {
                Title = "Amino Acid",
                Position = AxisPosition.Top,
                AbsoluteMinimum = 0,
                Minimum = 0,
                MajorTickSize = 0,
                MinorStep = 2,
                Angle = -90,
                MinorTickSize = 10,
                MaximumPadding = 0,
                FontSize = 10
            };
            this.PlotModel.Axes.Add(this.xaxis);

            // Init Y axis
            this.yaxis = new LinearAxis
            {
                Title = "Ion Type",
                Position = AxisPosition.Left,
                AbsoluteMinimum = 0,
                Minimum = 0,
                MajorStep = 1.0,
                MajorTickSize = 0,
                MinorStep = 0.5,
                MinorTickSize = 20,
                MaximumPadding = 0,
                FontSize = 10
            };
            this.PlotModel.Axes.Add(this.yaxis);

            // Init Color axis
            var minColor = OxyColor.FromRgb(127, 255, 0);
            var maxColor = OxyColor.FromRgb(255, 0, 0);
            this.colorAxis = new LinearColorAxis
            {
                Title = "Error",
                Position = AxisPosition.Right,
                AxisDistance = -0.5,
                AbsoluteMinimum = 0,
                Palette = OxyPalette.Interpolate(1000, minColor, maxColor),
                Minimum = -1 * IcParameters.Instance.ProductIonTolerancePpm.GetValue(),
                Maximum = IcParameters.Instance.ProductIonTolerancePpm.GetValue(),
                AbsoluteMaximum = IcParameters.Instance.ProductIonTolerancePpm.GetValue(),
                LowColor = OxyColors.Navy,
            };
            this.PlotModel.Axes.Add(this.colorAxis);

            this.WhenAnyValue(x => x.ShouldCombineChargeStates).Subscribe(_ => this.SetData(this.selectedSequence, this.selectedPeakDataPoints));

            // Save As Image Command requests a file path from the user and then saves the error map as an image
            var saveAsImageCommand = ReactiveCommand.Create();
            saveAsImageCommand.Subscribe(_ => this.SaveAsImageImpl());
            this.SaveAsImageCommand = saveAsImageCommand;

            this.SaveDataTableCommand = ReactiveCommand.Create();
            this.SaveDataTableCommand.Subscribe(_ => this.SaveDataTableImpl());
        }

        /// <summary>
        /// Gets the plot Model for error heat map
        /// </summary>
        public PlotModel PlotModel { get; private set; }

        /// <summary>
        /// Gets a command that prompts user for file path and save plot as image.
        /// </summary>
        public IReactiveCommand SaveAsImageCommand { get; private set; }

        /// <summary>
        /// Gets a command that prompt user for file path and save data table as list.
        /// </summary>
        public ReactiveCommand<object> SaveDataTableCommand { get; private set; }

        /// <summary>
        /// Gets or sets the data that is shown in the "Table" view. This excludes any fragments without data.
        /// </summary>
        public ReactiveList<PeakDataPoint> DataTable
        {
            get { return this.dataTable; }
            private set { this.RaiseAndSetIfChanged(ref this.dataTable, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether multiple charge states for an ion should
        /// be combined into a single row on the heat map.
        /// </summary>
        public bool ShouldCombineChargeStates
        {
            get { return this.shouldCombineChargeStates; }
            set { this.RaiseAndSetIfChanged(ref this.shouldCombineChargeStates, value); }
        }

        /// <summary>
        /// Gets the iontype selected from the error map.
        /// </summary>
        public string SelectedIonType
        {
            get { return this.selectedIonType; }
            private set { this.RaiseAndSetIfChanged(ref this.selectedIonType, value); }
        }

        /// <summary>
        /// Gets the residue and index of the seleted item.
        /// </summary>
        public string SelectedAminoAcid
        {
            get { return this.selectedAminoAcid; }
            private set { this.RaiseAndSetIfChanged(ref this.selectedAminoAcid, value); }
        }

        /// <summary>
        /// Gets the value of the selected item.
        /// </summary>
        public string SelectedValue
        {
            get { return this.selectedValue; }
            private set { this.RaiseAndSetIfChanged(ref this.selectedValue, value); }
        }

        /// <summary>
        /// Set sequence and data displayed on heat map.
        /// </summary>
        /// <param name="sequence">The sequence to display as the x axis of the plot.</param>
        /// <param name="peakDataPoints">The peak data points to extract error values from.</param>
        public void SetData(Sequence sequence, IEnumerable<IList<PeakDataPoint>> peakDataPoints)
        {
            this.selectedSequence = sequence;

            if (sequence == null || peakDataPoints == null)
            {
                return;
            }

            // Remove all points except for most abundant isotope peaks
            var dataPoints = this.GetMostAbundantIsotopePeaks(peakDataPoints).ToArray();

            // No data, nothing to do
            if (dataPoints.Length == 0)
            {
                return;
            }

            this.selectedPeakDataPoints = peakDataPoints;

            // Remove NaN values for data table (only showing fragment ions found in spectrum in data table)
            //this.DataTable = new ReactiveList<PeakDataPoint>(dataPoints.Where(dp => !dp.Error.Equals(double.NaN)));
            this.DataTable = new ReactiveList<PeakDataPoint>(dataPoints);

            // Build and invalidate erorr map plot display
            this.BuildErrorPlotModel(sequence, this.GetErrorDataArray(dataPoints, sequence.Count));
            this.PlotModel.MouseDown += this.MapMouseDown;
        }

        /// <summary>
        /// Build the error heat map
        /// </summary>
        /// <param name="sequence">The sequence to display as x axis on the plot</param>
        /// <param name="data">
        /// Data to be shown on the heat map.
        /// First dimension is sequence
        /// Second dimension is ion type
        /// </param>
        private void BuildErrorPlotModel(IReadOnlyList<AminoAcid> sequence, double[,] data)
        {
            // initialize color axis
            ////var minColor = OxyColor.FromRgb(127, 255, 0);
            this.colorAxis.Minimum = -1 * IcParameters.Instance.ProductIonTolerancePpm.GetValue();
            this.colorAxis.Maximum = IcParameters.Instance.ProductIonTolerancePpm.GetValue();
            this.colorAxis.AbsoluteMaximum = IcParameters.Instance.ProductIonTolerancePpm.GetValue();

            this.PlotModel.Series.Clear();

            // initialize heat map
            var heatMapSeries = new HeatMapSeries
            {
                Data = data,
                Interpolate = false,
                X0 = 1,
                X1 = data.GetLength(0),
                Y0 = 1,
                Y1 = data.GetLength(1),
                TrackerFormatString = 
                        ////"{1}: {2:0}" + Environment.NewLine +
                        ////"{3}: {4:0}" + Environment.NewLine +
                        "{5}: {6:0.###}ppm",
            };
            this.PlotModel.Series.Add(heatMapSeries);

            this.xaxis.AbsoluteMaximum = sequence.Count;
            this.yaxis.AbsoluteMaximum = this.ionTypes.Length;

            // Set yAxis double -> string label converter function
            this.yaxis.LabelFormatter = y =>
            {
                if (y.Equals(0))
                {
                    return string.Empty;
                }

                var ionType = this.ionTypes[Math.Min((int)y - 1, this.ionTypes.Length - 1)];
                var symbol = ionType.BaseIonType == null ? ionType.Name : ionType.BaseIonType.Symbol;
                return ionType.Charge == 1 ?
                       symbol :
                       string.Format("{0}({1}+)", symbol, ionType.Charge);
            };

            // Set xAxis double -> string label converter function
            this.xaxis.LabelFormatter = x =>
            {
                if (x.Equals(0))
                {
                    return string.Empty;
                }

                int sequenceIndex = Math.Max(Math.Min((int)x - 1, sequence.Count - 1), 0);
                string residue = sequence[sequenceIndex].Residue.ToString(CultureInfo.InvariantCulture);
                return string.Format("{0}{1}", residue, (int)x);
            };

            // Update plot
            this.PlotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// Organize the peak data points by ion type
        /// </summary>
        /// <param name="dataPoints">The data Points.</param>
        /// <param name="sequenceLength">The length of the sequence.</param>
        /// <returns>
        /// 2d array where first dimension is sequence and second dimension is ion type
        /// </returns>
        private double[,] GetErrorDataArray(IEnumerable<PeakDataPoint> dataPoints, int sequenceLength)
        {
            var dataDict = this.PartitionData(dataPoints, sequenceLength);

            this.ionTypes = dataDict.Keys.ToArray();

            var data = new double[sequenceLength, dataDict.Keys.Count];

            // create two dimensional array from partitioned data
            for (int i = 0; i < sequenceLength; i++)
            {
                for (int j = 0; j < this.ionTypes.Length; j++)
                {
                    var dataPoint = dataDict[this.ionTypes[j]][i];
                    var value = dataPoint == null ? double.NaN : dataPoint.Error;

                    if (value.Equals(double.NaN))
                    {
                        value = (-1 * IcParameters.Instance.ProductIonTolerancePpm.GetValue()) - 1;
                    }

                    data[i, j] = value;
                }
            }

            return data;
        }

        /// <summary>
        /// Partition data by ion type.
        /// Reduce the charge states to only one charge if <see cref="ShouldCombineChargeStates" /> is true.
        /// </summary>
        /// <param name="dataPoints">The data Points.</param>
        /// <param name="sequenceLength">The length of the sequence.</param>
        /// <returns>Peak data points partitioned by ion type.</returns>
        private Dictionary<IonType, PeakDataPoint[]> PartitionData(IEnumerable<PeakDataPoint> dataPoints, int sequenceLength)
        {
            var dataDict = new Dictionary<IonType, PeakDataPoint[]>();

            // partition data set by ion type
            foreach (var dataPoint in dataPoints)
            {
                var ionType = dataPoint.IonType;
                if (this.ShouldCombineChargeStates)
                {
                    ionType = new IonType(ionType.Name, ionType.OffsetComposition, 1, ionType.IsPrefixIon);
                }

                if (!dataDict.ContainsKey(ionType))
                {
                    dataDict.Add(ionType, new PeakDataPoint[sequenceLength]);
                }

                var points = dataDict[ionType];

                int index = dataPoint.Index - 1;

                if (!dataPoint.IonType.IsPrefixIon)
                {
                    index = sequenceLength - dataPoint.Index;
                }

                // If the ion type has multiple options, choose the best one.
                if (points[index] == null || 
                    double.IsNaN(points[index].Error) ||
                    (dataPoint.Y / Math.Abs(dataPoint.Error)) > (points[index].Y / Math.Abs(points[index].Error)))
                {
                    points[index] = dataPoint;
                }
            }

            return dataDict;
        }

        /// <summary>
        /// Get list of only most abundant isotope peaks of the ion peak data points
        /// Associate residue with the sequence
        /// </summary>
        /// <param name="peakDataPoints">Peak data points for ions on the spectrum plot</param>
        /// <returns>List of most abundant isotope peak data points</returns>
        private IEnumerable<PeakDataPoint> GetMostAbundantIsotopePeaks(IEnumerable<IList<PeakDataPoint>> peakDataPoints)
        {
            var mostAbundantPeaks = new ReactiveList<PeakDataPoint>();
            foreach (var peaks in peakDataPoints)
            {
                var peak = peaks.OrderByDescending(p => p.Y).FirstOrDefault();
                if (peak != null && peak.IonType != null && peak.IonType.Name != "Precursor")
                {
                    mostAbundantPeaks.Add(peak);
                }
            }

            return mostAbundantPeaks;
        }

        /// <summary>
        /// Prompt user for file path and save plot as image to that path.
        /// </summary>
        private void SaveAsImageImpl()
        {
            var filePath = this.dialogService.SaveFile(".png", @"Png Files (*.png)|*.png");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (directory == null || !Directory.Exists(directory))
                {
                    throw new FormatException(
                        string.Format("Cannot save image due to invalid file name: {0}", filePath));
                }

                DynamicResolutionPngExporter.Export(
                    this.PlotModel,
                    filePath,
                    (int)this.PlotModel.Width,
                    (int)this.PlotModel.Height,
                    OxyColors.White,
                    IcParameters.Instance.ExportImageDpi);
            }
            catch (Exception e)
            {
                this.dialogService.ExceptionAlert(e);
            }
        }

        /// <summary>
        /// Prompt user for file path and save data table as list.
        /// </summary>
        private void SaveDataTableImpl()
        {
            var filePath = this.dialogService.SaveFile(".tsv", @"Tab-separated value Files (*.tsv)|*.tsv");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (directory == null || !Directory.Exists(directory))
                {
                    throw new FormatException(
                        string.Format("Cannot save image due to invalid file name: {0}", filePath));
                }

                var peakExporter = new SpectrumPeakExporter(filePath);
                peakExporter.Export(this.DataTable.Where(p => !p.X.Equals(double.NaN)).ToList());
            }
            catch (Exception e)
            {
                this.dialogService.ExceptionAlert(e);
            }
        }

        /// <summary>
        /// Event handler for click event. Selects data based on cell clicked in heat map.
        /// </summary>
        /// <param name="sender">The sender heatmap plotmodel.</param>
        /// <param name="args">The event arguments.</param>
        private void MapMouseDown(object sender, OxyMouseEventArgs args)
        {
            var series = this.PlotModel.GetSeriesFromPoint(args.Position);
            var heatMap = series as HeatMapSeries;
            if (heatMap != null)
            {
                var point = heatMap.GetNearestPoint(args.Position, true);
                var dataPoint = point.DataPoint;

                var ionType = this.ionTypes[((int)dataPoint.Y) - 1];
                var seqIndex = ((int)dataPoint.X) - 1;
                var ionIndex = ionType.IsPrefixIon ? seqIndex + 1 : this.selectedSequence.Count - seqIndex - 1;
                var aminoAcid = this.selectedSequence[seqIndex];

                var value = point.Text;

                this.SelectedIonType = ionType.Name;
                this.SelectedAminoAcid = string.Format("{0}{1}", aminoAcid.Residue, ionIndex);

                if (value.Contains(" "))
                {
                    var parts = value.Split(' ');
                    value = parts[1];

                    int result;
                    var intPart = value.Split('p')[0];
                    if (Int32.TryParse(intPart, out result) && result < -1*IcParameters.Instance.ProductIonTolerancePpm.GetValue())
                    {
                        value = "N/A";
                    }
                }

                this.SelectedValue = value;
            }
        }
    }
}
