﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DatasetInfo.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   A class representing a dataset.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.Models.Dataset
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// A class representing a dataset.
    /// </summary>
    public class DatasetInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DatasetInfo"/> class.
        /// </summary>
        public DatasetInfo()
        {
            this.Name = string.Empty;
            this.Files = new List<FileInfo>();
            this.LayoutFile = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DatasetInfo"/> class.
        /// </summary>
        /// <param name="files">The files.</param>
        public DatasetInfo(IEnumerable<string> files)
        {
            this.Files = new List<FileInfo>();
            foreach (var file in files)
            {
                var fileInfo = FileInfo.GetFileInfo(file);
                if (fileInfo.FileType == FileTypes.SpectrumFile)
                {
                    this.Name = Path.GetFileName(fileInfo.FilePath);
                }

                this.Files.Add(fileInfo);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DatasetInfo"/> class.
        /// </summary>
        /// <param name="files">The files.</param>
        public DatasetInfo(IEnumerable<FileInfo> files)
        {
            this.Files = files.ToList();
            this.Name = this.Files.Where(f => f.FileType == FileTypes.SpectrumFile)
                .Select(f => Path.GetFileName(f.FilePath)).FirstOrDefault(fn => !string.IsNullOrWhiteSpace(fn));
        }

        /// <summary>
        /// Gets or sets the name of the dataset.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the list of files that are part of this dataset.
        /// </summary>
        public List<FileInfo> Files { get; set; }
        
        /// <summary>
        /// Gets or sets the path to the serialized layout file for this dataset.
        /// </summary>
        public string LayoutFile { get; set; }

        /// <summary>
        /// Get datasets from a collection of input file paths.
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public static DatasetInfo[] GetDatasetsFromInputFilePaths(IEnumerable<string> files)
        {
            var fileInfo = files.Select(FileInfo.GetFileInfo);
            var spectrumFiles = fileInfo.Where(f => f.FileType == FileTypes.SpectrumFile);
            var nameToFileSet = new Dictionary<string, List<FileInfo>>();
            var uniqueFiles = new HashSet<FileInfo>();
            foreach (var file in spectrumFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file.FilePath);
                if (!string.IsNullOrEmpty(name))
                {
                    var otherFiles = fileInfo.Where(f => f.FilePath.Contains(name) && f.FileType != FileTypes.SpectrumFile);
                    if (!nameToFileSet.ContainsKey(name))
                    {
                        nameToFileSet.Add(name, new List<FileInfo>());
                    }

                    uniqueFiles.Add(file);
                    nameToFileSet[name].Add(file);
 
                    foreach (var otherFile in otherFiles)
                    {
                        if (!uniqueFiles.Contains(otherFile))
                        {
                            uniqueFiles.Add(otherFile);
                            nameToFileSet[name].Add(file);
                        }
                    }
                }
            }

            return nameToFileSet.Values.Select(fileSet => new DatasetInfo(fileSet)).ToArray();
        }

        /// <summary>
        /// Finds the spectrum file path from the file list.
        /// </summary>
        /// <returns>The path for the spectrum file.</returns>
        public string GetSpectrumFilePath()
        {
            return this.Files.Where(file => file.FileType == FileTypes.SpectrumFile)
                             .Select(file => file.FilePath).FirstOrDefault();
        }

        /// <summary>
        /// Gets the file paths to the FASTA files associated with this dataset.
        /// </summary>
        /// <returns>Array of FASTA file paths.</returns>
        public string[] GetFastaFilePaths()
        {
            return this.Files.Where(file => file.FileType == FileTypes.FastaFile)
                             .Select(file => file.FilePath).ToArray();

        }
    }
}