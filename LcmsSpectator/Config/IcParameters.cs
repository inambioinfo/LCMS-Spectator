﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IcParameters.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   Represents the type of precursor ions displayed.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.Config
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using InformedProteomics.Backend.Data.Composition;
    using InformedProteomics.Backend.Data.Enum;
    using InformedProteomics.Backend.Data.Sequence;
    using InformedProteomics.Backend.Data.Spectrometry;

    using OxyPlot;

    using ReactiveUI;

    /// <summary>
    /// Represents the type of precursor ions displayed.
    /// </summary>
    public enum PrecursorViewMode
    {
        /// <summary>
        /// Isotopes of the precursor ion are displayed.
        /// </summary>
        Isotopes,

        /// <summary>
        /// Neighboring charge states of the precursor ion are displayed.
        /// </summary>
        Charges
    }

    /// <summary>
    /// This class contains settings fields for the LCMSSpectator Application.
    /// </summary>
    public class IcParameters : ReactiveObject
    {
        /// <summary>
        /// The singleton object instance of the <see cref="IcParameters" /> class.
        /// </summary>
        private static IcParameters instance;

        /// <summary>
        /// The Tolerance value used for finding precursor ions in the application.
        /// </summary>
        private Tolerance precursorTolerancePpm;

        /// <summary>
        /// Tolerance value used for finding product ions in the application.
        /// </summary>
        private Tolerance productIonTolerance;

        /// <summary>
        /// The minimum pearson correlation for ions displayed in MS/MS spectra.
        /// </summary>
        private double ionCorrelationThreshold;

        /// <summary>
        /// The default value to use for the <see cref="SavitzkyGolaySmoother" /> in XIC plot smoothing slider.
        /// </summary>
        private int pointsToSmooth;

        /// <summary>
        /// The minimum relative intensity for isotopes of precursor ions displayed.
        /// </summary>
        private double precursorRelativeIntensityThreshold;

        /// <summary>
        /// A value indicating whether the charge and mass reported by the instrument be stored in the <see cref="PrSm" />s.
        /// </summary>
        private bool showInstrumentData;

        /// <summary>
        /// The type of precursor ions displayed in the application.
        /// </summary>
        private PrecursorViewMode precursorViewMode;

        /// <summary>
        /// The list of static modifications used by the application.
        /// </summary>
        private List<SearchModification> searchModifications;

        /// <summary>
        /// The modifications that are applied to the light sequence when ShowHeavy is selected.
        /// </summary>
        private ReactiveList<SearchModification> lightModifications;

        /// <summary>
        /// The modifications that are applied to the heavy sequence when ShowHeavy is selected.
        /// </summary>
        private ReactiveList<SearchModification> heavyModifications;

        /// <summary>
        /// The factory for obtaining <see cref="IonType" />s.
        /// </summary>
        private IonTypeFactory ionTypeFactory;

        /// <summary>
        /// Factory for obtaining deconvoluted <see cref="IonType" />s.
        /// </summary>
        private IonTypeFactory deconvolutedIonTypeFactory;

        /// <summary>
        /// A value that indicates whether ion types should automatically be selected base on the activation method
        /// of the currently select MS/MS spectrum.
        /// </summary>
        private bool automaticallySelectIonTypes;

        /// <summary>
        /// The ion types that are automatically selected when the activation method of the
        /// selected MS/MS spectrum is CID or HCD.
        /// </summary>
        private List<BaseIonType> cidBaseIonTypes;

        /// <summary>
        /// The ion types that are automatically selected when the activation method of the
        /// selected MS/MS spectrum is ETD.
        /// </summary>
        private List<BaseIonType> etdIonTypes;

        /// <summary>
        /// The colors for the feature on the feature map.
        /// </summary>
        private OxyColor[] featureColors;

        /// <summary>
        /// The colors for the ID points on the feature map.
        /// </summary>
        private OxyColor[] idColors;

        /// <summary>
        /// The colors for the MS/MS scan points on the feature map.
        /// </summary>
        private OxyColor ms2ScanColor;

        /// <summary>
        /// Prevents a default instance of the <see cref="IcParameters"/> class from being created.
        /// Instantiate default values of LCMSSpectator application settings.
        /// </summary>
        private IcParameters()
        {
            this.PrecursorTolerancePpm = new Tolerance(10, ToleranceUnit.Ppm);
            this.ProductIonTolerancePpm = new Tolerance(10, ToleranceUnit.Ppm);
            this.IonCorrelationThreshold = 0.7;
            this.PointsToSmooth = 9;
            this.PrecursorRelativeIntensityThreshold = 0.1;
            this.ShowInstrumentData = false;
            PrecursorViewMode = PrecursorViewMode.Isotopes;
            this.SearchModifications = new List<SearchModification>
            {
                new SearchModification(Modification.Carbamidomethylation, 'C', SequenceLocation.Everywhere, true)
            };

            this.LightModifications = new ReactiveList<SearchModification>();
            this.HeavyModifications = new ReactiveList<SearchModification>
            {
                new SearchModification(Modification.LysToHeavyLys, 'K', SequenceLocation.PeptideCTerm, true),
                new SearchModification(Modification.ArgToHeavyArg, 'R', SequenceLocation.PeptideCTerm, true)
            };
            IonTypeFactory = new IonTypeFactory(100);
            this.DeconvolutedIonTypeFactory = IonTypeFactory.GetDeconvolutedIonTypeFactory(
                                                                                      BaseIonType.AllBaseIonTypes,
                                                                                      NeutralLoss.CommonNeutralLosses);
            this.AutomaticallySelectIonTypes = true;
            this.CidHcdIonTypes = new List<BaseIonType> { BaseIonType.B, BaseIonType.Y };
            this.EtdIonTypes = new List<BaseIonType> { BaseIonType.C, BaseIonType.Z };

            this.RegisteredModifications = new ReactiveList<Modification>(Modification.CommonModifications)
            {
                Modification.LysToHeavyLys,
                Modification.ArgToHeavyArg
            };

            this.ExportImageDpi = 96;

            this.FeatureColors = new[] { OxyColors.Black, OxyColors.Blue, OxyColors.Orange, OxyColors.Red };
            this.IdColors = new[]
            {
                OxyColors.LightGreen, OxyColors.LightBlue, OxyColors.Turquoise, OxyColors.Olive,
                OxyColors.Brown, OxyColors.Cyan, OxyColors.Gray, OxyColors.Pink, OxyColors.LightSeaGreen,
                OxyColors.Beige
            };

            this.Ms2ScanColor = OxyColors.OliveDrab;
        }

        /// <summary>
        /// Event that is triggered when the settings have been updated.
        /// </summary>
        public event Action IcParametersUpdated;

        /// <summary>
        /// Gets the singleton object instance of the <see cref="IcParameters" /> class.
        /// </summary>
        public static IcParameters Instance
        {
            get { return instance ?? (instance = new IcParameters()); }
        }

        /// <summary>
        /// Gets or sets the Tolerance value used for finding precursor ions in the application.
        /// </summary>
        public Tolerance PrecursorTolerancePpm
        {
            get { return this.precursorTolerancePpm; }
            set { this.RaiseAndSetIfChanged(ref this.precursorTolerancePpm, value); }
        }

        /// <summary>
        /// Gets or sets the tolerance value used for finding product ions in the application.
        /// </summary>
        public Tolerance ProductIonTolerancePpm
        {
            get { return this.productIonTolerance; }
            set { this.RaiseAndSetIfChanged(ref this.productIonTolerance, value); }
        }

        /// <summary>
        /// Gets or sets the minimum pearson correlation for ions displayed in MS/MS spectra.
        /// </summary>
        public double IonCorrelationThreshold
        {
            get { return this.ionCorrelationThreshold; }
            set { this.RaiseAndSetIfChanged(ref this.ionCorrelationThreshold, value); }
        }

        /// <summary>
        /// Gets or sets the default value to use for the <see cref="SavitzkyGolaySmoother" /> in XIC plot smoothing slider.
        /// </summary>
        public int PointsToSmooth
        {
            get { return this.pointsToSmooth; }
            set { this.RaiseAndSetIfChanged(ref this.pointsToSmooth, value); }
        }

        /// <summary>
        /// Gets or sets the minimum relative intensity for isotopes of precursor ions displayed.
        /// </summary>
        public double PrecursorRelativeIntensityThreshold
        {
            get { return this.precursorRelativeIntensityThreshold; }
            set { this.RaiseAndSetIfChanged(ref this.precursorRelativeIntensityThreshold, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the charge and mass reported by the instrument be stored in the <see cref="PrSm" />s.
        /// </summary>
        public bool ShowInstrumentData
        {
            get { return this.showInstrumentData; }
            set { this.RaiseAndSetIfChanged(ref this.showInstrumentData, value); }
        }

        /// <summary>
        /// Gets or sets the type of precursor ions displayed in the application.
        /// </summary>
        public PrecursorViewMode PrecursorViewMode
        {
            get { return this.precursorViewMode; }
            set { this.RaiseAndSetIfChanged(ref this.precursorViewMode, value); }
        }

        /// <summary>
        /// Gets or sets the list of static modifications used by the application.
        /// </summary>
        public List<SearchModification> SearchModifications
        {
            get { return this.searchModifications; }
            set { this.RaiseAndSetIfChanged(ref this.searchModifications, value); }
        }

        /// <summary>
        /// Gets or sets the modifications that are applied to the light sequence when ShowHeavy is selected.
        /// </summary>
        public ReactiveList<SearchModification> LightModifications
        {
            get { return this.lightModifications; }
            set { this.RaiseAndSetIfChanged(ref this.lightModifications, value); }
        }

        /// <summary>
        /// Gets or sets the modifications that are applied to the heavy sequence when ShowHeavy is selected.
        /// </summary>
        public ReactiveList<SearchModification> HeavyModifications
        {
            get { return this.heavyModifications; }
            set { this.RaiseAndSetIfChanged(ref this.heavyModifications, value); }
        }

        /// <summary>
        /// Gets the factory for obtaining <see cref="IonType" />s.
        /// </summary>
        public IonTypeFactory IonTypeFactory
        {
            get { return this.ionTypeFactory; }
            private set { this.RaiseAndSetIfChanged(ref this.ionTypeFactory, value); }
        }

        /// <summary>
        /// Gets the factory for obtaining deconvoluted <see cref="IonType" />s.
        /// </summary>
        public IonTypeFactory DeconvolutedIonTypeFactory
        {
            get { return this.deconvolutedIonTypeFactory; }
            private set { this.RaiseAndSetIfChanged(ref this.deconvolutedIonTypeFactory, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether ion types should automatically be selected base on the activation method
        /// of the currently select MS/MS spectrum.
        /// </summary>
        public bool AutomaticallySelectIonTypes
        {
            get { return this.automaticallySelectIonTypes; }
            set { this.RaiseAndSetIfChanged(ref this.automaticallySelectIonTypes, value); }
        }

        /// <summary>
        /// Gets or sets the ion types that are automatically selected when the activation method of the
        /// selected MS/MS spectrum is CID or HCD.
        /// </summary>
        public List<BaseIonType> CidHcdIonTypes
        {
            get { return this.cidBaseIonTypes; }
            set { this.RaiseAndSetIfChanged(ref this.cidBaseIonTypes, value); }
        }

        /// <summary>
        /// Gets or sets the ion types that are automatically selected when the activation method of the
        /// selected MS/MS spectrum is ETD.
        /// </summary>
        public List<BaseIonType> EtdIonTypes
        {
            get { return this.etdIonTypes; }
            set { this.RaiseAndSetIfChanged(ref this.etdIonTypes, value); }
        }

        /// <summary>
        /// Gets or sets the dots-per-inch for exported images.
        /// </summary>
        public int ExportImageDpi { get; set; }

        /// <summary>
        /// Gets or sets the colors for the features on the feature map.
        /// </summary>
        public OxyColor[] FeatureColors
        {
            get { return this.featureColors; }
            set { this.RaiseAndSetIfChanged(ref this.featureColors, value); }
        }

        /// <summary>
        /// Gets or sets the colors for the ID points on the feature map.
        /// </summary>
        public OxyColor[] IdColors
        {
            get { return this.idColors; }
            set { this.RaiseAndSetIfChanged(ref this.idColors, value); }
        }

        /// <summary>
        /// Gets or sets the color for the MS/MS scan points on the feature map.
        /// </summary>
        public OxyColor Ms2ScanColor
        {
            get { return this.ms2ScanColor; }
            set { this.RaiseAndSetIfChanged(ref this.ms2ScanColor, value); }
        }

        /// <summary>
        /// Gets the modifications that are registered with LCMSSpectator.
        /// </summary>
        public ReactiveList<Modification> RegisteredModifications { get; private set; }

        /// <summary>
        /// Parse a list of base ion type symbols separated by spaces.
        /// </summary>
        /// <param name="str">String containing ion type symbols separated by spaces.</param>
        /// <returns>List of BaseIonTypes representing each ion type in the string.</returns>
        public static List<BaseIonType> IonTypeStringParse(string str)
        {
            if (str == "*")
            {
                return BaseIonType.AllBaseIonTypes.ToList();
            }

            var parts = str.Split(' ');
            var ionNames = BaseIonType.AllBaseIonTypes.ToDictionary(bit => bit.Symbol, bit => bit);

            return (from part in parts where ionNames.ContainsKey(part) select ionNames[part]).ToList();
        }

        /// <summary>
        /// Get IonType from IonTypeFactory.
        /// </summary>
        /// <param name="baseIonType">Base ion type of ion type to get.</param>
        /// <param name="neutralLoss">Neutral loss of ion type to get.</param>
        /// <param name="charge">Charge of ion type to get</param>
        /// <returns>IonType with the given base ion type, neutral loss, and charge.</returns>
        public IonType GetIonType(BaseIonType baseIonType, NeutralLoss neutralLoss, int charge)
        {
            var chargeStr = charge.ToString(CultureInfo.InvariantCulture);
            if (charge == 1)
            {
                chargeStr = string.Empty;
            }

            var name = baseIonType.Symbol + chargeStr + neutralLoss.Name;
            return IonTypeFactory.GetIonType(name);
        }

        /// <summary>
        /// Trigger settings updated event.
        /// </summary>
        public void Update()
        {
            if (this.IcParametersUpdated != null)
            {
                this.IcParametersUpdated();
            }
        }

        /// <summary>
        /// Get string containing list of ion types to be highlighted for CID and HCD spectra.
        /// </summary>
        /// <returns>String containing ion type symbols separated by spaces.</returns>
        public string GetCidHcdIonTypes()
        {
            if (this.CidHcdIonTypes.Count == BaseIonType.AllBaseIonTypes.Count())
            {
                return "*";
            }

            return this.CidHcdIonTypes.Aggregate(string.Empty, (current, ionType) => current + ionType.Symbol + " ");
        }

        /// <summary>
        /// Get string containing list of ion types to be highlighted for ETD spectra.
        /// </summary>
        /// <returns>String containing ion type symbols separated by spaces.</returns>
        public string GetEtdIonTypes()
        {
            if (this.EtdIonTypes.Count == BaseIonType.AllBaseIonTypes.Count())
            {
                return "*";
            }

            return this.EtdIonTypes.Aggregate(string.Empty, (current, ionType) => current + ionType.Symbol + " ");
        }

        /// <summary>
        /// Register a new modification with the application given an empirical formula.
        /// </summary>
        /// <param name="modName">Name of modification.</param>
        /// <param name="composition">Empirical formula of modification.</param>
        /// <returns>The modification that was registered with the application.</returns>
        public Modification RegisterModification(string modName, Composition composition)
        {
            var mod = Modification.RegisterAndGetModification(modName, composition);
            this.RegisteredModifications.Add(mod);
            return mod;
        }

        /// <summary>
        /// Register a new modification with the application given a delta mass shift.
        /// </summary>
        /// <param name="modName">Name of modification.</param>
        /// <param name="mass">Delta mass of modification.</param>
        /// <returns>The modification that was registered with the application.</returns>
        public Modification RegisterModification(string modName, double mass)
        {
            var mod = Modification.RegisterAndGetModification(modName, mass);
            this.RegisteredModifications.Add(mod);
            return mod;
        }

        /// <summary>
        /// Update or register a modification.
        /// </summary>
        /// <param name="modName">The name of the modification.</param>
        /// <param name="composition">The composition of the modification.</param>
        /// <returns>The registered modification.</returns>
        public Modification UpdateOrRegisterModification(string modName, Composition composition)
        {
            var mod = Modification.UpdateAndGetModification(modName, composition);
            var regMod = this.RegisteredModifications.FirstOrDefault(m => m.Name == modName);
            if (regMod != null)
            {
                this.RegisteredModifications.Remove(regMod);
            }

            this.RegisteredModifications.Add(mod);
            return mod;
        }

        /// <summary>
        /// Update or register a modification.
        /// </summary>
        /// <param name="modName">The name of the modification.</param>
        /// <param name="mass">The mass of the modification.</param>
        /// <returns>The registered modification.</returns>
        public Modification UpdateOrRegisterModification(string modName, double mass)
        {
            var mod = Modification.UpdateAndGetModification(modName, mass);
            var regMod = this.RegisteredModifications.FirstOrDefault(m => m.Name == modName);
            if (regMod != null)
            {
                this.RegisteredModifications.Remove(regMod);
            }

            this.RegisteredModifications.Add(mod);
            return mod;
        }

        /// <summary>
        /// Unregister a modification.
        /// </summary>
        /// <param name="modification">The modification to unregister.</param>
        public void UnregisterModification(Modification modification)
        {
            Modification.UnregisterModification(modification);
            if (this.RegisteredModifications.Contains(modification))
            {
                this.RegisteredModifications.Remove(modification);
            }
        }
    }
}
