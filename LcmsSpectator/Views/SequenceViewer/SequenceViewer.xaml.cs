﻿using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LcmsSpectator.DialogServices;

namespace LcmsSpectator.Views.SequenceViewer
{
    using System;
    using System.Windows.Controls;

    using LcmsSpectator.ViewModels.SequenceViewer;

    using ReactiveUI;

    /// <summary>
    /// Interaction logic for SequenceViewer.xaml
    /// </summary>
    public partial class SequenceViewer : UserControl
    {
        public SequenceViewer()
        {
            InitializeComponent();
        }

        public void WriteToPng(string filePath)
        {
            var target = this.SequenceContainer;
            if (target == null)
            {
                return;
            }
            Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(bounds.Width),
                                                            (int)(bounds.Height), 96, 96,
                                                            PixelFormats.Pbgra32);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(target);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
            }
            rtb.Render(dv);
            var bmp = rtb;

            //RenderTargetBitmap bmp = new RenderTargetBitmap((int)this.ActualWidth, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);

            var encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bmp));

            using (Stream stm = File.Create(filePath))
                encoder.Save(stm);
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialogService = new DialogService();
            var filePath = dialogService.SaveFile(".png", @"Png Files (*.png)|*.png");
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                this.WriteToPng(filePath);
            }
        }
    }
}
