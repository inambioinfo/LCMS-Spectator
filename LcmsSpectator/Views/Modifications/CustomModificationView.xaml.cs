﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CustomModificationView.xaml.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   Interaction logic for CustomModificationView.xaml
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.Views.Modifications
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for CustomModificationView.xaml
    /// </summary>
    public partial class CustomModificationView : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomModificationView"/> class.
        /// </summary>
        public CustomModificationView()
        {
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(SelectivelyHandleMouseButton), true);
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.GotKeyboardFocusEvent, new RoutedEventHandler(SelectAllText), true);
            this.InitializeComponent();
        }

        /// <summary>
        /// Event handler for PreviewLeftButtonDownEvent.
        /// Causes the sending TextBox to select all of its contents when focused (when clicked).
        /// </summary>
        /// <param name="sender">The sender TextBox.</param>
        /// <param name="e">The event arguments.</param>
        private static void SelectivelyHandleMouseButton(object sender, MouseButtonEventArgs e)
        {
            var textbox = sender as TextBox;
            if (textbox != null && !textbox.IsKeyboardFocusWithin)
            {
                e.Handled = true; // disable default behavior.
                textbox.Focus();
            }
        }

        /// <summary>
        /// Event handler for GotKeyBoardFocusEvent
        /// Causes the sending TextBox to select all of its contents when focused (when tabbed to).
        /// </summary>
        /// <param name="sender">The sender TextBox.</param>
        /// <param name="e">The event arguments.</param>
        private static void SelectAllText(object sender, RoutedEventArgs e)
        {
            var textBox = e.OriginalSource as TextBox;
            if (textBox != null)
            {
                textBox.SelectAll();
            }
        }
    }
}
