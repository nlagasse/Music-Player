using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace musicplayer.Services
{
    public static class ImageService
    {
        public static List<string> GetAlbumArtPaths(string folderPath)
        {
            List<string> imageFiles = Directory.GetFiles(folderPath)
            .Where(file =>
                file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file)
            .ToList();

            if (imageFiles.Count == 0)
                return new List<string>();

            string? frontCover = imageFiles.FirstOrDefault(file =>
                Path.GetFileNameWithoutExtension(file).Equals("cover", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileNameWithoutExtension(file).Equals("front", StringComparison.OrdinalIgnoreCase));

            string? backCover = imageFiles.FirstOrDefault(file =>
                Path.GetFileNameWithoutExtension(file).Equals("back", StringComparison.OrdinalIgnoreCase));

            List<string> sortedArt = new List<string>();

            if (frontCover != null)
            {
                sortedArt.Add(frontCover);
            }
            else
            {
                sortedArt.Add(imageFiles[0]);
            }

            if (backCover != null && backCover != sortedArt[0])
            {
                sortedArt.Add(backCover);
            }
            else
            {
                string? secondImage = imageFiles.FirstOrDefault(file => file != sortedArt[0]);

                if (secondImage != null)
                    sortedArt.Add(secondImage);
            }

            foreach (string imageFile in imageFiles)
            {
                if (!sortedArt.Contains(imageFile))
                    sortedArt.Add(imageFile);
            }

            return sortedArt;
        }

        public static BitmapImage? LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (!File.Exists(path))
                return null;

            BitmapImage image = new BitmapImage();

            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            return image;
        }
    }
}
