using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace musicplayer.Services
{
    public static class MetadataCleanup
    {
        public static string CleanAlbumTitle(string title, string artist, uint releaseYear)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            string originalTitle = title.Trim();
            string cleanedTitle = originalTitle;

            if (!string.IsNullOrWhiteSpace(artist))
            {
                string escapedArtist = Regex.Escape(artist.Trim());

                // Artist | Year | Album
                // Artist - Year - Album
                // Artist_Year_Album
                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"^\s*" + escapedArtist + @"\s*[-–—|_]+\s*(19|20)\d{2}\s*[-–—|_]+\s*",
                    "",
                    RegexOptions.IgnoreCase
                );

                // Artist - Album
                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"^\s*" + escapedArtist + @"\s*[-–—|_]+\s*",
                    "",
                    RegexOptions.IgnoreCase
                );

                // Album - Artist
                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"\s*[-–—|_]+\s*" + escapedArtist + @"\s*$",
                    "",
                    RegexOptions.IgnoreCase
                );
            }

            // Remove years in brackets/parentheses:
            // (2005) Album -> Album
            // Album (2005) -> Album
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"\s*[\[\(]\s*(19|20)\d{2}\s*[\]\)]\s*",
                " ",
                RegexOptions.IgnoreCase
            );

            // Remove year at beginning:
            // 2003 - Album
            // 2003 Album
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"^\s*(19|20)\d{2}\s*[-–—|_]*\s*",
                "",
                RegexOptions.IgnoreCase
            );

            // Remove year at end:
            // Album - 2003
            // Album 2003
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"\s*[-–—|_]*\s*(19|20)\d{2}\s*$",
                "",
                RegexOptions.IgnoreCase
            );

            // Clean leftover separators at beginning/end.
            cleanedTitle = Regex.Replace(cleanedTitle, @"^\s*[-–—|_]+\s*", "");
            cleanedTitle = Regex.Replace(cleanedTitle, @"\s*[-–—|_]+\s*$", "");

            cleanedTitle = Regex.Replace(cleanedTitle, @"\s{2,}", " ").Trim();

            // Self-titled fallback:
            // If removing artist/year made the title empty, keep artist as album title.
            if (string.IsNullOrWhiteSpace(cleanedTitle) && !string.IsNullOrWhiteSpace(artist))
                cleanedTitle = artist.Trim();

            return cleanedTitle;
        }

        public static string CleanSongTitle(string title, string artist)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            string originalTitle = title.Trim();
            string cleanedTitle = originalTitle;

            // Removes leading track numbers like:
            // "1. Song"
            // "01 - Song"
            // "03_ Song"
            // "9) Song"
            // "1 Song"
            //
            // But if the whole title is just "4", keep it.
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"^\s*\d+\s*(?:[._)-]\s*|\s+)",
                ""
            );

            if (string.IsNullOrWhiteSpace(cleanedTitle))
                cleanedTitle = originalTitle;

            if (!string.IsNullOrWhiteSpace(artist))
            {
                string escapedArtist = Regex.Escape(artist.Trim());

                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"^\s*" + escapedArtist + @"\s*[-–—_]\s*",
                    "",
                    RegexOptions.IgnoreCase
                );

                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"\s*[-–—_]\s*" + escapedArtist + @"\s*$",
                    "",
                    RegexOptions.IgnoreCase
                );
            }

            if (string.IsNullOrWhiteSpace(cleanedTitle))
                cleanedTitle = originalTitle;

            return cleanedTitle.Trim();
        }

        public static bool IsAudioFile(string filePath)
        {
            return filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".flac", StringComparison.OrdinalIgnoreCase);
        }

    }
}
