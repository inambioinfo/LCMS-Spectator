﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProteinId.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   This class is a container for PRSMs that have the same protein identified.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using InformedProteomics.Backend.Data.Sequence;
    using InformedProteomics.Backend.MassSpecData;

    /// <summary>
    /// This class is a container for PRSMs that have the same protein identified.
    /// </summary>
    public class ProteinId : IIdData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProteinId"/> class.
        /// </summary>
        /// <param name="sequence">The unmodified complete protein sequence.</param>
        /// <param name="proteinName">The protein name.</param>
        public ProteinId(Sequence sequence, string proteinName)
        {
            Sequence = sequence;
            this.ProteinName = proteinName;
            this.Proteoforms = new Dictionary<string, ProteoformId>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProteinId"/> class. 
        /// Constructor for creating a ProteinId from a FASTA Entry.
        /// </summary>
        /// <param name="fastaEntry">The FASTA Entry.</param>
        public ProteinId(FastaEntry fastaEntry)
        {
            this.Proteoforms = new Dictionary<string, ProteoformId>();
            this.Sequence = new Sequence(fastaEntry.ProteinSequenceText, new AminoAcidSet());
            this.ProteinName = fastaEntry.ProteinName;
            this.ProteinDescription = fastaEntry.ProteinDescription;
        }

        /// <summary>
        /// Gets the unmodified complete protein sequence.
        /// </summary>
        public Sequence Sequence { get; private set; }

        /// <summary>
        /// Gets the protein name.
        /// </summary>
        public string ProteinName { get; private set; }

        /// <summary>
        /// Gets the description of the protein.
        /// </summary>
        public string ProteinDescription { get; private set; }

        /// <summary>
        /// Gets a dictionary that maps a protein name to a ProteinID.
        /// </summary>
        public Dictionary<string, ProteoformId> Proteoforms { get; private set; }

        /// <summary>
        /// Add a Protein-Spectrum-Match identification.
        /// </summary>
        /// <param name="id">Protein-Spectrum-Match to add</param>
        public void Add(PrSm id)
        {
            if (!this.Proteoforms.ContainsKey(id.SequenceText))
            {
                this.Proteoforms.Add(id.SequenceText, new ProteoformId(id, this.Sequence));
            }

            var proteoform = this.Proteoforms[id.SequenceText];
            proteoform.Add(id);
        }

        /// <summary>
        /// Remove a Protein-Spectrum-Match identification.
        /// </summary>
        /// <param name="id">Protein-Spectrum-Match to remove.</param>
        public void Remove(PrSm id)
        {
            if (this.Proteoforms.ContainsKey(id.SequenceText))
            {
                this.Proteoforms[id.SequenceText].Remove(id);
            }
        }

        public void ClearIds()
        {
            this.Proteoforms.Clear();
        }

        /// <summary>
        /// Get the PRSM in the tree with the highest score.
        /// </summary>
        /// <returns>The PRSM with the highest score.</returns>
        public PrSm GetHighestScoringPrSm()
        {
            PrSm highest = null;
            foreach (var proteoform in this.Proteoforms.Values)
            {
                var highestProtein = proteoform.GetHighestScoringPrSm();
                if (highest == null || highestProtein.CompareTo(highest) >= 0)
                {
                    highest = highestProtein;
                }
            }

            return highest;
        }

        /// <summary>
        /// Set the LCMSRun for all data.
        /// </summary>
        /// <param name="lcms">LCMSRun to set.</param>
        /// <param name="dataSetName">Name of the data this for the LCMSRun.</param>
        public void SetLcmsRun(ILcMsRun lcms, string dataSetName)
        {
            foreach (var proteoform in this.Proteoforms.Values)
            {
                proteoform.SetLcmsRun(lcms, dataSetName);
            }
        }

        /// <summary>
        /// Determines whether the item contains a given identification.
        /// </summary>
        /// <param name="id">the ID to search for.</param>
        /// <returns>A value indicating whether the item contains the identification.</returns>
        public bool Contains(PrSm id)
        {
            return this.Proteoforms.Values.Any(proteoform => proteoform.Contains(id));
        }

        /// <summary>
        /// Remove all PRSMs that are part of a certain data set.
        /// </summary>
        /// <param name="rawFileName">Name of the data set.</param>
        public void RemovePrSmsFromRawFile(string rawFileName)
        {
            var newProteoforms = new Dictionary<string, ProteoformId>();
            foreach (var proteoform in this.Proteoforms)
            {
                proteoform.Value.RemovePrSmsFromRawFile(rawFileName);
                if (proteoform.Value.ChargeStates.Count > 0)
                {
                    newProteoforms.Add(proteoform.Key, proteoform.Value);
                }
            }

            this.Proteoforms = newProteoforms;
        }

        /// <summary>
        /// Comparison class for comparing ProteinID by protein name.
        /// </summary>
        public class ProteinIdNameDescComparer : IComparer<ProteinId>
        {            
            /// <summary>
            /// Compare two ProteinIDs by protein name.
            /// </summary>
            /// <param name="x">Left protein.</param>
            /// <param name="y">Right protein.</param>
            /// <returns>
            /// Integer value indicating whether the left protein is 
            /// less than, equal to, or greater than right protein.
            /// </returns>
            public int Compare(ProteinId x, ProteinId y)
            {
                return string.Compare(x.ProteinName, y.ProteinName, StringComparison.Ordinal);
            }
        }
    }
}
