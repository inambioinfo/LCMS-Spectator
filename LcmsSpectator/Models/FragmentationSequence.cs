﻿namespace LcmsSpectator.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using InformedProteomics.Backend.Data.Biology;
    using InformedProteomics.Backend.Data.Composition;
    using InformedProteomics.Backend.Data.Sequence;
    using InformedProteomics.Backend.Data.Spectrometry;
    using InformedProteomics.Backend.MassSpecData;
    using LcmsSpectator.Utils;
    using LcmsSpectator.ViewModels.Data;
    using Splat;

    public class FragmentationSequence
    {
        /// <summary>
        /// Lock for thread-safe access to caches.
        /// </summary>
        private readonly object cacheLock;

        /// <summary>
        /// Cache for previously calculated fragment ions for a particular composition and ion type.
        /// </summary>
        private readonly MemoizingMRUCache<Tuple<Composition, IonType>, LabeledIonViewModel> fragmentCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="FragmentationSequence"/> class.
        /// </summary>
        /// <param name="sequence">The underlying sequence.</param>
        /// <param name="charge">Charge of sequence.</param>
        /// <param name="lcms">The LCMSRun for the data set.</param>
        /// <param name="activationMethod">The Activation Method.</param>
        public FragmentationSequence(Sequence sequence, int charge, ILcMsRun lcms, ActivationMethod activationMethod)
        {
            this.cacheLock = new object();
            this.fragmentCache = new MemoizingMRUCache<Tuple<Composition, IonType>, LabeledIonViewModel>(this.GetLabeledIonViewModel, 1000);

            this.Sequence = sequence;
            this.Charge = charge;
            this.LcMsRun = lcms;
            this.ActivationMethod = activationMethod;
        }

        /// <summary>
        /// Gets the underlying sequence.
        /// </summary>
        public Sequence Sequence { get; private set; }

        /// <summary>
        /// Gets the charge.
        /// </summary>
        public int Charge { get; private set; }

        /// <summary>
        /// Gets the LCMSRun for the data set.
        /// </summary>
        public ILcMsRun LcMsRun { get; private set; }

        /// <summary>
        /// Gets the Activation Method.
        /// </summary>
        public ActivationMethod ActivationMethod { get; private set; }

        /// <summary>
        /// Get fragment ion labels.
        /// </summary>
        /// <param name="ionTypes">List of IonTypes.</param>
        /// <param name="labelModifications">The heavy/light labels.</param>
        /// <returns>A list of fragment labeled ions.</returns>
        public Task<List<LabeledIonViewModel>> GetFragmentLabelsAsync(IList<IonType> ionTypes, SearchModification[] labelModifications = null)
        {
            return Task.Run(() => this.GetFragmentLabels(ionTypes, labelModifications));
        }

        /// <summary>
        /// Get isotope ion labels for precursor.
        /// </summary>
        /// <param name="relativeIntensityThreshold">Relative intensity threshold (fraction of most abundant isotope)</param>
        /// <param name="labelModifications">The heavy/light labels.</param>
        /// <returns>A list of precursor labeled ions.</returns>
        public Task<List<LabeledIonViewModel>> GetIsotopePrecursorLabelsAsync(
            double relativeIntensityThreshold = 0.1,
            IEnumerable<SearchModification> labelModifications = null)
        {
            return Task.Run(() => this.GetIsotopePrecursorLabels(relativeIntensityThreshold, labelModifications));
        }

        /// <summary>
        /// Get neighboring charge state ion labels for precursor.
        /// </summary>
        /// <param name="labelModifications">The heavy/light labels.</param>
        /// <returns>A list of neighboring charge state labeled ions.</returns>
        public Task<List<LabeledIonViewModel>> GetChargePrecursorLabelsAsync(
            IEnumerable<SearchModification> labelModifications = null)
        {
            return Task.Run(() => this.GetChargePrecursorLabels(labelModifications));
        }

        /// <summary>
        /// Calculate fragment ion labels.
        /// </summary>
        /// <param name="ionTypes">List of IonTypes.</param>
        /// <param name="labelModifications">The heavy/light labels.</param>
        /// <returns>A list of fragment labeled ions.</returns>
        public List<LabeledIonViewModel> GetFragmentLabels(IList<IonType> ionTypes, SearchModification[] labelModifications = null)
        {
            var fragmentLabelList = new List<LabeledIonViewModel> { Capacity = this.Sequence.Count * ionTypes.Count * this.Charge };
            if (this.Sequence.Count < 1 || this.LcMsRun == null)
            {
                return fragmentLabelList;
            }

            var sequence = labelModifications == null ? this.Sequence : IonUtils.GetHeavySequence(this.Sequence, labelModifications);

            var precursorIon = IonUtils.GetPrecursorIon(sequence, this.Charge);
            lock (this.cacheLock)
            {
                foreach (var ionType in ionTypes)
                {
                    var ionFragments = new List<LabeledIonViewModel>();
                    for (int i = 1; i < this.Sequence.Count; i++)
                    {
                        var startIndex = ionType.IsPrefixIon ? 0 : i;
                        var length = ionType.IsPrefixIon ? i : sequence.Count - i;
                        var fragment = new Sequence(this.Sequence.GetRange(startIndex, length));
                        var ions = ionType.GetPossibleIons(fragment);

                        foreach (var ion in ions)
                        {
                            var labeledIonViewModel = this.fragmentCache.Get(new Tuple<Composition, IonType>(ion.Composition, ionType));
                            labeledIonViewModel.Index = length;
                            labeledIonViewModel.PrecursorIon = precursorIon;

                            ionFragments.Add(labeledIonViewModel);
                        }

                        if (!ionType.IsPrefixIon)
                        {
                            ionFragments.Reverse();
                        }
                    }

                    fragmentLabelList.AddRange(ionFragments);
                }
            }

            return fragmentLabelList;
        }

        /// <summary>
        /// Calculate isotope ion labels for precursor.
        /// </summary>
        /// <param name="relativeIntensityThreshold">Relative intensity threshold (fraction of most abundant isotope)</param>
        /// <param name="labelModifications">The heavy/light labels.</param>
        /// <returns>A list of precursor labeled ions.</returns>
        public List<LabeledIonViewModel> GetIsotopePrecursorLabels(double relativeIntensityThreshold = 0.1, IEnumerable<SearchModification> labelModifications = null)
        {
            var ions = new List<LabeledIonViewModel>();
            if (this.Sequence.Count == 0 || this.LcMsRun == null)
            {
                return ions;
            }

            var sequence = this.Sequence;
            if (labelModifications != null)
            {
                sequence = IonUtils.GetHeavySequence(sequence, labelModifications.ToArray());
            }

            #pragma warning disable 0618
            var precursorIonType = new IonType("Precursor", Composition.H2O, this.Charge, false);
            #pragma warning restore 0618
            var composition = sequence.Aggregate(Composition.Zero, (current, aa) => current + aa.Composition);
            var relativeIntensities = composition.GetIsotopomerEnvelope();
            var indices = new List<int> { -1 };
            for (int i = 0; i < relativeIntensities.Envelope.Length; i++)
            {
                if (relativeIntensities.Envelope[i] >= relativeIntensityThreshold || i == 0)
                {
                    indices.Add(i);
                }
            }

            ions.AddRange(indices.Select(index => new LabeledIonViewModel(composition, precursorIonType, false, this.LcMsRun, null, false, index)));
            return ions;
        }

        /// <summary>
        /// Calculate neighboring charge state ion labels for precursor.
        /// </summary>
        /// <param name="labelModifications">The heavy/light labels.</param>
        /// <returns>A list of neighboring charge state labeled ions.</returns>
        public List<LabeledIonViewModel> GetChargePrecursorLabels(IEnumerable<SearchModification> labelModifications = null)
        {
            var ions = new List<LabeledIonViewModel>();
            var numChargeStates = IonUtils.GetNumNeighboringChargeStates(this.Charge);
            if (this.Sequence.Count == 0 || this.LcMsRun == null)
            {
                return ions;
            }

            var sequence = this.Sequence;
            if (labelModifications != null)
            {
                sequence = IonUtils.GetHeavySequence(sequence, labelModifications.ToArray());
            }

            var composition = sequence.Aggregate(Composition.Zero, (current, aa) => current + aa.Composition);
            var minCharge = Math.Max(1, this.Charge - numChargeStates);
            var maxCharge = this.Charge + numChargeStates;

            for (int i = minCharge; i <= maxCharge; i++)
            {
                var index = i - minCharge;
                if (index == 0)
                {
                    index = this.Charge - minCharge;
                }

                if (i == this.Charge)
                {
                    index = 0;         // guarantee that actual charge is index 0
                }

                #pragma warning disable 0618
                var precursorIonType = new IonType("Precursor", Composition.H2O, i, false);
                #pragma warning restore 0618
                ions.Add(new LabeledIonViewModel(composition, precursorIonType, false, this.LcMsRun, null, true, index));
            }

            return ions;
        }

        /// <summary>
        /// Calculate a fragment ion label.
        /// </summary>
        /// <param name="key">The key consisting of empirical formula (composition) and ion type.</param>
        /// <param name="ob">The ob required for cache access.</param>
        /// <returns>A fragment labeled ion.</returns>
        private LabeledIonViewModel GetLabeledIonViewModel(Tuple<Composition, IonType> key, object ob)
        {
            return new LabeledIonViewModel(key.Item1, key.Item2, true, this.LcMsRun);
        }
    }
}
