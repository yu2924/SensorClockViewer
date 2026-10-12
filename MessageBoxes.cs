using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace SensorClockViewer
{
    static class MessageBoxes
	{
		public static async Task ShowAsync(UIElement uie, string title, string message)
		{
			var dialog = new ContentDialog
			{
				XamlRoot = uie.XamlRoot,
				Title = title,
				Content = message,
				CloseButtonText = "OK",
			};
			await dialog.ShowAsync();
		}
		public static async Task<ContentDialogResult> ShowConfirmAsync(UIElement uie, string title, string message, string btntext)
		{
			var dialog = new ContentDialog
			{
				XamlRoot = uie.XamlRoot,
				Title = title,
				Content = message,
				PrimaryButtonText = btntext,
				CloseButtonText = "Cancel",
				DefaultButton = ContentDialogButton.Primary
			};
			return await dialog.ShowAsync();
		}
	}
}
