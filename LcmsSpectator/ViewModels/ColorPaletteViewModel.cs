﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorPaletteViewModel.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   View model for a color palette.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Media;
    using ReactiveUI;

    /// <summary>
    /// View model for a color palette.
    /// </summary>
    public class ColorPaletteViewModel : ReactiveObject
    {
        /// <summary>
        /// The title of the color palette.
        /// </summary>
        private string title;

        /// <summary>
        /// A value indicating whether this palette has been selected.
        /// </summary>
        private bool isSelected;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorPaletteViewModel"/> class.
        /// </summary>
        /// <param name="colors">The colors to initialize the palette with.</param>
        public ColorPaletteViewModel(IEnumerable<Color> colors = null)
        {
            this.Colors = new ReactiveList<Color>();

            if (colors != null)
            {
                this.Colors.AddRange(colors);
            }

            var paletteSelectedCommand = ReactiveCommand.Create();
            paletteSelectedCommand.Subscribe(_ => this.IsSelected = true);
            this.PaletteSelectedCommand = paletteSelectedCommand;
        }

        /// <summary>
        /// Gets or sets the title of the color palette.
        /// </summary>
        public string Title
        {
            get { return this.title; }
            set { this.RaiseAndSetIfChanged(ref this.title, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this palette has been selected.
        /// </summary>
        public bool IsSelected
        {
            get { return this.isSelected; }
            set { this.RaiseAndSetIfChanged(ref this.isSelected, value); }
        }

        /// <summary>
        /// Gets the colors for the palette.
        /// </summary>
        public ReactiveList<Color> Colors { get; private set; }

        /// <summary>
        /// Gets a command that marks the palette as selected.
        /// </summary>
        public IReactiveCommand PaletteSelectedCommand { get; private set; }
    }
}
