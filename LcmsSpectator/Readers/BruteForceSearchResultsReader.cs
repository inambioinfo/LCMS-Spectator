﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IcFileReader.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   Reader for MSPathFinder results file.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.Readers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading.Tasks;
    using InformedProteomics.Backend.Data.Sequence;
    using LcmsSpectator.Models;
    using LcmsSpectator.Readers.SequenceReaders;

    /// <summary>
    /// Reader for MSPathFinder results file.
    /// </summary>
    public class BruteForceSearchResultsReader : IIdFileReader
    {
        /// <summary>
        /// The path to the TSV file.
        /// </summary>
        private readonly string filePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="LcmsSpectator.Readers.IcFileReader"/> class.
        /// </summary>
        /// <param name="filePath">The path to the TSV file.</param>
        public BruteForceSearchResultsReader(string filePath)
        {
            this.filePath = filePath;
            this.Modifications = new List<Modification>();
        }

        /// <summary>
        /// Read a MSPathFinder results file.
        /// </summary>
        /// <param name="modIgnoreList">Ignores modifications contained in this list.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <returns><returns>The Protein-Spectrum-Match identifications.</returns></returns>
        public IEnumerable<PrSm> Read(IEnumerable<string> modIgnoreList = null, IProgress<double> progress = null)
        {
            var modIgnore = modIgnoreList == null ? new List<string>() : new List<string>(modIgnoreList);
            return this.ReadFile(modIgnore).Result;
        }

        /// <summary>
        /// Read a MSPathFinder results file asynchronously.
        /// </summary>
        /// <param name="modIgnoreList">Ignores modifications contained in this list.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>Identification tree of MSPathFinder identifications.</returns>
        public async Task<IEnumerable<PrSm>> ReadAsync(IEnumerable<string> modIgnoreList = null, IProgress<double> progress = null)
        {
            var modIgnore = modIgnoreList == null ? new List<string>() : new List<string>(modIgnoreList);
            return await this.ReadFile(modIgnore);
        }

        public IList<Modification> Modifications { get; private set; }

        /// <summary>
        /// Read a MSPathFinder results file.
        /// </summary>
        /// <param name="modIgnoreList">Ignores modifications contained in this list.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>The Protein-Spectrum-Match identifications.</returns>
        private async Task<IEnumerable<PrSm>> ReadFile(List<string> modIgnoreList = null, IProgress<double> progress = null)
        {
            progress = progress ?? new Progress<double>();
            var ext = Path.GetExtension(this.filePath);
            IEnumerable<PrSm> prsms;
            if (ext == ".tsv")
            {
                var file = new StreamReader(File.Open(this.filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                prsms = await this.ReadTsv(file, modIgnoreList, progress);
            }
            else if (ext == ".zip")
            {
                prsms = await this.ReadZip(modIgnoreList, progress);
            }
            else
            {
                throw new ArgumentException(string.Format("Cannot read file with extension \"{0}\"", ext));
            }

            return prsms;
        }

        /// <summary>
        /// Read a MSPathFinder results from TSV file.
        /// </summary>
        /// <param name="stream">The stream for an open TSV file.</param>
        /// <param name="modIgnoreList">Ignores modifications contained in this list. </param>
        /// <returns>The Protein-Spectrum-Match identifications.</returns>
        private async Task<IEnumerable<PrSm>> ReadTsv(StreamReader stream, List<string> modIgnoreList, IProgress<double> progress)
        {
            ////var file = File.ReadLines(_tsvFile);
            var prsms = new List<PrSm>();
            var headers = new Dictionary<string, int>();
            var lineCount = 0;
            while (!stream.EndOfStream)
            {
                var line = await stream.ReadLineAsync();
                lineCount++;
                if (lineCount == 1 && line != null)
                { // first line
                    var parts = line.Split('\t');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        headers.Add(parts[i], i);
                    }

                    continue;
                }

                var idData = this.CreatePrSms(line, headers, modIgnoreList);
                if (idData != null)
                {
                    prsms.AddRange(idData);
                }
            }

            stream.Close();
            return prsms;
        }

        /// <summary>
        /// Read a MSPathFinder results from GZipped TSV file.
        /// </summary>
        /// <param name="modIgnoreList">Ignores modifications contained in this list.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>Task that creates an identification tree of MSPathFinder identifications.</returns>
        private async Task<IEnumerable<PrSm>> ReadZip(List<string> modIgnoreList, IProgress<double> progress)
        {
            var zipFilePath = this.filePath;
            var fileName = Path.GetFileNameWithoutExtension(zipFilePath);
            if (fileName != null && fileName.EndsWith("_IcTsv"))
            {
                fileName = fileName.Substring(0, fileName.Length - 6);
            }

            var tsvFileName = string.Format("{0}_IcTda.tsv", fileName);

            var zipArchive = ZipFile.OpenRead(zipFilePath);
            var entry = zipArchive.GetEntry(tsvFileName);
            if (entry != null)
            {
                using (var fileStream = new StreamReader(entry.Open()))
                {
                    return await this.ReadTsv(fileStream, modIgnoreList, progress);
                }
            }

            return new List<PrSm>();
        }

        /// <summary>
        /// Create Protein-Spectrum-Matches identification from a line of the results file.
        /// </summary>
        /// <param name="line">Single line of the results file.</param>
        /// <param name="headers">Headers of the TSV file.</param>
        /// <param name="modIgnoreList">Ignores modifications contained in this list.</param>
        /// <returns>List of Protein-Spectrum-Match identifications.</returns>
        private IEnumerable<PrSm> CreatePrSms(string line, IReadOnlyDictionary<string, int> headers, IEnumerable<string> modIgnoreList)
        {
            var expectedHeaders = new List<string>
            {
                "Score",
                "Protein",
                "Description",
                "Sequence",
                "Scan",
            };

            foreach (var header in expectedHeaders.Where(header => !headers.ContainsKey(header)))
            {
                throw new KeyNotFoundException(string.Format("Missing expected column header \"{0}\" in ID file.", header));
            }

            var parts = line.Split('\t');
            var scoreLabel = "Score";

            var score = Convert.ToDouble(parts[headers[scoreLabel]]);

            var proteinNames = parts[headers["Protein"]].Split(';');
            var prsms = new List<PrSm> { Capacity = proteinNames.Length };

            if (modIgnoreList != null)
            {
                if (modIgnoreList.Select(mod => string.Format("{0} ", mod)).Any(searchMod => parts[headers["Modifications"]].Contains(searchMod)))
                {
                    return null;
                }
            }

            var sequenceReader = new SequenceReader();

            foreach (var protein in proteinNames)
            {
                var prsm = new PrSm(sequenceReader)
                {
                    Heavy = false,
                    Scan = Convert.ToInt32(parts[headers["Scan"]]),
                    Charge = 1,
                    ////Sequence = sequenceData.Item1,
                    SequenceText = parts[headers["Sequence"]],
                    ProteinName = protein,
                    ProteinDesc = parts[headers["Description"]].Split(';').FirstOrDefault(),
                    Score = Math.Round(score, 3),
                };
                prsms.Add(prsm);
            }

            return prsms;
        }

        /// <summary>
        /// Exception thrown for unknown or modifications.
        /// </summary>
        public class InvalidModificationNameException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LcmsSpectator.Readers.IcFileReader.InvalidModificationNameException"/> class. 
            /// </summary>
            /// <param name="message">Exception message.</param>
            /// <param name="modificationName">The name of the invalid modification.</param>
            public InvalidModificationNameException(string message, string modificationName)
                : base(message)
            {
                this.ModificationName = modificationName;
            }

            /// <summary>
            /// Gets the name of the invalid modification.
            /// </summary>
            public string ModificationName { get; private set; }
        }
    }
}