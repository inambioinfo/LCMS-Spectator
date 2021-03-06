﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SpectrumViewModel.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   View model for displaying MS and MS/MS spectrum plots.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.ViewModels.Plots
{
    using System;
    using System.Linq;
    using System.Reactive.Linq;

    using InformedProteomics.Backend.Data.Spectrometry;
    using InformedProteomics.Backend.MassSpecData;

    using LcmsSpectator.Config;
    using LcmsSpectator.DialogServices;
    using LcmsSpectator.Models;
    using LcmsSpectator.ViewModels.Data;

    using OxyPlot.Axes;

    using ReactiveUI;

    /// <summary>
    /// View model for displaying MS and MS/MS spectrum plots.
    /// </summary>
    public class SpectrumViewModel : ReactiveObject
    {
        /// <summary>
        /// LCMSRun for the data set that the spectrum plots are part of.
        /// </summary>
        private readonly ILcMsRun lcms;

        /// <summary>
        /// The spectrum plot view model for MS/MS spectrum.
        /// </summary>
        private SpectrumPlotViewModel ms2SpectrumViewModel;

        /// <summary>
        /// The spectrum plot view model for previous MS1 spectrum.
        /// </summary>
        private SpectrumPlotViewModel prevMs1SpectrumViewModel;

        /// <summary>
        /// The spectrum plot view model for next MS1 spectrum.
        /// </summary>
        private SpectrumPlotViewModel nextMs1SpectrumViewModel;

        /// <summary>
        /// The view model for the spectrum shown in the primary spectrum plot view
        /// </summary>
        private SpectrumPlotViewModel primarySpectrumViewModel;

        /// <summary>
        /// The view model for the spectrum shown in the first secondary spectrum plot view
        /// </summary>
        private SpectrumPlotViewModel secondary1ViewModel;

        /// <summary>
        /// The view model for the spectrum shown in the second secondary spectrum plot view
        /// </summary>
        private SpectrumPlotViewModel secondary2ViewModel;

        /// <summary>
        /// A value indicating whether or not the change to an axis was caused
        /// by synchronizing axes.
        /// </summary>
        private bool isAxisInternalChange;

        /// <summary>
        /// The fragmentation sequence (fragment/precursor ion generator)
        /// </summary>
        private FragmentationSequence fragmentationSequence;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpectrumViewModel"/> class.
        /// </summary>
        /// <param name="dialogService">Dialog service for opening dialogs from view model.</param>
        /// <param name="lcms">LCMSRun for the data set that the spectrum plots are part of.</param>
        public SpectrumViewModel(IMainDialogService dialogService, ILcMsRun lcms)
        {
            this.lcms = lcms;
            this.Ms2SpectrumViewModel = new SpectrumPlotViewModel(dialogService, new FragmentationSequenceViewModel(), 1.05);
            this.PrevMs1SpectrumViewModel = new SpectrumPlotViewModel(
                dialogService,
                new PrecursorSequenceIonViewModel { PrecursorViewMode = PrecursorViewMode.Charges },
                1.25,
                false);
            this.NextMs1SpectrumViewModel = new SpectrumPlotViewModel(
                dialogService,
                new PrecursorSequenceIonViewModel { PrecursorViewMode = PrecursorViewMode.Charges },
                1.25,
                false);

            // When prev ms1 spectrum plot is zoomed/panned, next ms1 spectrum plot should zoom/pan
            this.isAxisInternalChange = false;
            this.PrevMs1SpectrumViewModel.XAxis.AxisChanged += (o, e) =>
                {
                    if (this.isAxisInternalChange)
                    {
                        return;
                    }

                    this.isAxisInternalChange = true;
                    this.NextMs1SpectrumViewModel.XAxis.Zoom(this.PrevMs1SpectrumViewModel.XAxis.ActualMinimum, this.PrevMs1SpectrumViewModel.XAxis.ActualMaximum);
                this.isAxisInternalChange = false;
            };

            // When next ms1 spectrum plot is zoomed/panned, prev ms1 spectrum plot should zoom/pan
            this.NextMs1SpectrumViewModel.XAxis.AxisChanged += (o, e) =>
                {
                    if (this.isAxisInternalChange)
                    {
                        return;
                    }

                    this.isAxisInternalChange = true;
                    this.PrevMs1SpectrumViewModel.XAxis.Zoom(this.NextMs1SpectrumViewModel.XAxis.ActualMinimum, this.NextMs1SpectrumViewModel.XAxis.ActualMaximum);
                this.isAxisInternalChange = false;
            };

            this.WhenAnyValue(x => x.FragmentationSequence)
                .Where(fragSeq => fragSeq != null)
                .Subscribe(fragSeq =>
                {
                    this.Ms2SpectrumViewModel.FragmentationSequenceViewModel.FragmentationSequence = fragSeq;
                    this.PrevMs1SpectrumViewModel.FragmentationSequenceViewModel.FragmentationSequence = fragSeq;
                    this.NextMs1SpectrumViewModel.FragmentationSequenceViewModel.FragmentationSequence = fragSeq;
                });

            // By default, MS2 Spectrum is shown in the primary view
            this.PrimarySpectrumViewModel = this.Ms2SpectrumViewModel;
            this.Secondary1ViewModel = this.PrevMs1SpectrumViewModel;
            this.Secondary2ViewModel = this.NextMs1SpectrumViewModel;

            // Wire commands to swap the spectrum that is shown in the primary view
            var swapSecondary1Command = ReactiveCommand.Create();
            swapSecondary1Command.Subscribe(_ => this.SwapSecondary1());
            this.SwapSecondary1Command = swapSecondary1Command;

            var swapSecondary2Command = ReactiveCommand.Create();
            swapSecondary2Command.Subscribe(_ => this.SwapSecondary2());
            this.SwapSecondary2Command = swapSecondary2Command;
        }

        /// <summary>
        /// Gets a command that swaps the first secondary view model with primary view model.
        /// </summary>
        public IReactiveCommand SwapSecondary1Command { get; private set; }

        /// <summary>
        /// Gets a command that swaps the second secondary view model with primary view model.
        /// </summary>
        public IReactiveCommand SwapSecondary2Command { get; private set; }

        /// <summary>
        /// Gets the spectrum plot view model for MS/MS spectrum.
        /// </summary>
        public SpectrumPlotViewModel Ms2SpectrumViewModel
        {
            get { return this.ms2SpectrumViewModel; }
            private set { this.RaiseAndSetIfChanged(ref this.ms2SpectrumViewModel, value); }
        }

        /// <summary>
        /// Gets the spectrum plot view model for previous MS1 spectrum.
        /// </summary>
        public SpectrumPlotViewModel PrevMs1SpectrumViewModel
        {
            get { return this.prevMs1SpectrumViewModel; }
            private set { this.RaiseAndSetIfChanged(ref this.prevMs1SpectrumViewModel, value); }
        }

        /// <summary>
        /// Gets the spectrum plot view model for next MS1 spectrum.
        /// </summary>
        public SpectrumPlotViewModel NextMs1SpectrumViewModel
        {
            get { return this.nextMs1SpectrumViewModel; }
            private set { this.RaiseAndSetIfChanged(ref this.nextMs1SpectrumViewModel, value); }
        }

        /// <summary>
        /// Gets the view model for the spectrum shown in the primary spectrum plot view
        /// </summary>
        public SpectrumPlotViewModel PrimarySpectrumViewModel
        {
            get { return this.primarySpectrumViewModel; }
            private set { this.RaiseAndSetIfChanged(ref this.primarySpectrumViewModel, value); }
        }

        /// <summary>
        /// Gets the view model for the spectrum shown in the first secondary spectrum plot view
        /// </summary>
        public SpectrumPlotViewModel Secondary1ViewModel
        {
            get { return this.secondary1ViewModel; }
            private set { this.RaiseAndSetIfChanged(ref this.secondary1ViewModel, value); }
        }

        /// <summary>
        /// Gets the view model for the spectrum shown in the second secondary spectrum plot view
        /// </summary>
        public SpectrumPlotViewModel Secondary2ViewModel
        {
            get { return this.secondary2ViewModel; }
            private set { this.RaiseAndSetIfChanged(ref this.secondary2ViewModel, value); }
        }

        /// <summary>
        /// Gets or sets the fragmentation sequence (fragment/precursor ion generator)
        /// </summary>
        public FragmentationSequence FragmentationSequence
        {
            get { return this.fragmentationSequence; }
            set { this.RaiseAndSetIfChanged(ref this.fragmentationSequence, value); }
        }

        /// <summary>
        /// Update the spectrum plots to show a new set of spectra.
        /// </summary>
        /// <param name="scan">The scan number of the primary spectrum to display</param>
        /// <param name="precursorMz">The precursor M/Z of the ID displayed</param>
        public void UpdateSpectra(int scan, double precursorMz = 0)
        {
            if (scan == 0 || this.lcms == null)
            {
                return;
            }

            var primary = this.lcms.GetSpectrum(scan);

            string primaryTitle;
            string secondary1Title;
            string secondary2Title;

            Spectrum secondary1;
            Spectrum secondary2;

            if (primary is ProductSpectrum)
            {
                // The primary spectrum we want to show is an MS/MS spectrum
                primaryTitle = "MS/MS Spectrum";
                secondary1Title = "Previous Ms1 Spectrum";
                secondary2Title = "Next Ms1 Spectrum";
                secondary1 = this.lcms.GetSpectrum(this.lcms.GetPrevScanNum(scan, 1));
                secondary2 = this.lcms.GetSpectrum(this.lcms.GetNextScanNum(scan, 1));
            }
            else
            {
                // The primary spectrum that we want to show is a MS1 spectrum
                if (this.lcms != null)
                {
                    primary = this.FindNearestMs2Spectrum(scan, precursorMz);
                }

                if (primary == null)
                {
                    // no ms2 spectrum found
                    primary = this.lcms.GetSpectrum(scan);
                    primaryTitle = "MS1 Spectrum";
                    secondary1Title = string.Empty;
                    secondary2Title = string.Empty;
                    secondary1 = null;
                    secondary2 = null;
                }
                else if (primary.ScanNum < scan)
                {
                    // The primary spectrum scan is above the ms/ms spectrum scan that we selected
                    primaryTitle = "Previous MS/MS Spectrum";
                    secondary1Title = "Previous Ms1 Spectrum";
                    secondary2Title = "Ms1 Spectrum";
                    secondary1 = this.lcms.GetSpectrum(this.lcms.GetPrevScanNum(primary.ScanNum, 1));
                    secondary2 = this.lcms.GetSpectrum(scan);
                }
                else
                {
                    // The primary spectrum scan is below the ms/ms spectrum scan that we selected
                    primaryTitle = "Next MS/MS Spectrum";
                    secondary1Title = "MS1 Spectrum";
                    secondary2Title = "Next MS1 Spectrum";
                    secondary1 = this.lcms.GetSpectrum(scan);
                    secondary2 = this.lcms.GetSpectrum(this.lcms.GetNextScanNum(primary.ScanNum, 1));
                }
            }

            // Ms2 spectrum plot
            this.Ms2SpectrumViewModel.Title = string.Format("{0} (Scan: {1})", primaryTitle, primary.ScanNum);
            this.Ms2SpectrumViewModel.Spectrum = primary;

            // previous Ms1
            this.SetMs1XAxis(this.PrevMs1SpectrumViewModel.XAxis, primary, secondary1);
            this.PrevMs1SpectrumViewModel.Spectrum = secondary1;
            this.PrevMs1SpectrumViewModel.Title = secondary1 == null ? string.Empty : string.Format("{0} (Scan: {1})", secondary1Title, secondary1.ScanNum);
            
            // next Ms1
            this.SetMs1XAxis(this.Secondary2ViewModel.XAxis, primary, secondary1);
            this.NextMs1SpectrumViewModel.Spectrum = secondary2;
            this.NextMs1SpectrumViewModel.Title = secondary2 == null ? string.Empty : string.Format("{0} (Scan: {1})", secondary2Title, secondary2.ScanNum);
        }

        /// <summary>
        /// Swap the spectrum that is shown in the primary view with spectrum shown in the
        /// first secondary view
        /// </summary>
        public void SwapSecondary1()
        {
            var primary = this.PrimarySpectrumViewModel;
            var secondary = this.Secondary1ViewModel;
            this.PrimarySpectrumViewModel = null;
            this.Secondary1ViewModel = null;
            this.PrimarySpectrumViewModel = secondary;
            this.Secondary1ViewModel = primary;
        }

        /// <summary>
        /// Swap the spectrum that is shown in the primary view with spectrum shown in the
        /// second secondary view
        /// </summary>
        public void SwapSecondary2()
        {
            var primary = this.PrimarySpectrumViewModel;
            var secondary = this.Secondary2ViewModel;
            this.PrimarySpectrumViewModel = null;
            this.Secondary2ViewModel = null;
            this.PrimarySpectrumViewModel = secondary;
            this.Secondary2ViewModel = primary;
        }

        /// <summary>
        /// Set minimum and maximum values for shared XAxis for MS1 spectra plots
        /// </summary>
        /// <param name="xaxis">X Axis to set min and max values for.</param>
        /// <param name="ms2">MS/MS spectrum to get isolation window bounds from.</param>
        /// <param name="ms1">The MS1 that the plot for this axis displays</param>
        private void SetMs1XAxis(Axis xaxis, Spectrum ms2, Spectrum ms1)
        {
            var ms2Prod = ms2 as ProductSpectrum;
            if (ms2Prod == null || ms1 == null)
            {
                return;
            }

            var ms1AbsMax = ms1.Peaks.Max().Mz;
            var ms1Min = ms2Prod.IsolationWindow.MinMz;
            var ms1Max = ms2Prod.IsolationWindow.MaxMz;
            var diff = ms1Max - ms1Min;
            var ms1MinMz = ms2Prod.IsolationWindow.MinMz - (0.25 * diff);
            var ms1MaxMz = ms2Prod.IsolationWindow.MaxMz + (0.25 * diff);
            xaxis.Minimum = ms1MinMz;
            xaxis.Maximum = ms1MaxMz;
            xaxis.AbsoluteMinimum = 0;
            xaxis.AbsoluteMaximum = ms1AbsMax;
            xaxis.Zoom(ms1MinMz, ms1MaxMz);
        }

        /// <summary>
        /// Attempt to find the nearest MS/MS spectrum given an MS1 spectrum.
        /// </summary>
        /// <param name="ms1Scan">MS1 scan number</param>
        /// <param name="precursorMz">Precursor M/Z that should be in MS/MS spectrum's isolation window range.</param>
        /// <returns>Product spectrum for the nearest MS/MS spectrum. Returns null if one cannot be found.</returns>
        private ProductSpectrum FindNearestMs2Spectrum(int ms1Scan, double precursorMz)
        {
            var lcmsRun = this.lcms as LcMsRun;

            // Do not have a valid LCMSRun or PrecursorMz, so we're not going to find an ms2 spectrum.
            if (lcmsRun == null || precursorMz.Equals(0))
            {
                return null;
            }

            var scans = lcmsRun.GetFragmentationSpectraScanNums(precursorMz);
            if (scans.Length == 0)
            {
                return null;
            }

            var index = Array.BinarySearch(scans, ms1Scan);
            index = index < 0 ? ~index : index;
            var lowIndex = Math.Max(0, index - 1);
            var highIndex = Math.Min(scans.Length - 1, index + 1);

            var lowDiff = Math.Abs(index - lowIndex);
            var highDiff = Math.Abs(highIndex - index);

            var lowSpec = this.lcms.GetSpectrum(scans[lowIndex]) as ProductSpectrum;
            var highSpec = this.lcms.GetSpectrum(scans[highIndex]) as ProductSpectrum;

            ProductSpectrum spectrum;

            if ((lowDiff < highDiff || (lowDiff == highDiff && lowDiff > 0)) && lowSpec != null)
            {
                spectrum = lowSpec;
            }
            else if (highDiff < lowDiff && highSpec != null)
            {
                spectrum = highSpec;
            }
            else
            {
                spectrum = null;
            }

            return spectrum;
        }
    }
}
