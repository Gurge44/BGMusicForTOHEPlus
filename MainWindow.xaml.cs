using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SimpleMusicPlayer
{
    public partial class MainWindow : Window
    {
        private readonly MediaPlayer mediaPlayer = new();
        private readonly ObservableCollection<string> playlist = [];
        private readonly List<string> lastChosenSongs = [];
        private readonly DispatcherTimer positionTimer;
        private readonly Random random = new();
        private bool isShuffleEnabled = false;
        private bool isPlaying = false;

        public MainWindow()
        {
            InitializeComponent();
            playlistView.ItemsSource = playlist;

            // Load embedded music files
            LoadEmbeddedSongs();

            positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            positionTimer.Tick += PositionTimer_Tick;
            positionTimer.Start();

            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

            mediaPlayer.MediaFailed += (sender, args) => { MessageBox.Show($"Media failed to open: {args.ErrorException.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); };

            // Handle the MediaEnded event to play the next song
            mediaPlayer.MediaEnded += (sender, args) => { PlayNextSong(); };

            // Play the first song in the playlist
            PlayFirstSong();
        }

        private void LoadEmbeddedSongs()
        {
            try
            {
                // Get the names of all embedded resources in the "Tracks" folder
                string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();

                // Iterate through the resources
                foreach (string resourceName in resourceNames)
                {
                    if (resourceName.StartsWith("SimpleMusicPlayer.Tracks"))
                    {
                        // Extract the file name
                        string fileName = resourceName["SimpleMusicPlayer.Tracks.".Length..];

                        // Add the song to the playlist
                        playlist.Add(fileName);
                    }
                }

                // Set the DataContext of the ListView to the playlist
                playlistView.DataContext = playlist;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading embedded music files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlayNextSong()
        {
            string nextSong = isShuffleEnabled ? GetRandomSong() : GetNextSongInOrder();

            // Check if the next song has been played recently
            if (!lastChosenSongs.Contains(nextSong))
            {
                PlaySong(nextSong);

                playlistView.SelectedIndex = playlist.IndexOf(nextSong);

                // Add the played song to the list
                lastChosenSongs.Add(nextSong);

                // Keep the list length to a maximum of 10
                if (lastChosenSongs.Count > 10)
                {
                    lastChosenSongs.RemoveAt(0);
                }
            }
            else
            {
                // If the next song was played recently, try again
                PlayNextSong();
            }
        }

        private string GetNextSongInOrder()
        {
            // Ensure the playlist is not empty
            if (playlist.Count == 0)
            {
                MessageBox.Show("Playlist is empty.");
                return string.Empty;
            }

            // Find the index of the current song in the playlist
            int currentIndex = playlistView.SelectedIndex;

            // If the current song is the last one, play the first one; otherwise, play the next one
            int nextIndex = (currentIndex == playlist.Count - 1) ? 0 : currentIndex + 1;

            return playlist[nextIndex];
        }

        private string GetRandomSong()
        {
            // Ensure the playlist is not empty
            if (playlist.Count == 0)
            {
                MessageBox.Show("Playlist is empty.");
                return string.Empty;
            }

            // Select a random song from the playlist
            int index = random.Next(0, playlist.Count);
            return playlist[index];
        }

        // Event handler for the toggle button or checkbox for shuffle
        private void ShuffleCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            isShuffleEnabled = true;
        }

        private void ShuffleCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            isShuffleEnabled = false;
        }

        private void PlayFirstSong()
        {
            if (playlist.Count > 0)
            {
                string firstSong = playlist[0];
                PlaySong(firstSong);
            }
        }

        private void PlaySong(string selectedSong)
        {
            try
            {
                // Load the embedded resource stream
                Stream? resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"SimpleMusicPlayer.Tracks.{selectedSong}");

                if (resourceStream == null)
                {
                    MessageBox.Show($"Error: Resource stream is null for {selectedSong}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create a temporary file path
                string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp3");

                // Write the embedded resource to the temporary file
                using (FileStream fileStream = File.Create(tempFilePath))
                {
                    resourceStream.CopyTo(fileStream);
                }

                // Play the temporary file
                mediaPlayer.Open(new Uri(tempFilePath));
                mediaPlayer.Play();

                isPlaying = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing the song: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SkipSongButton_Click(object sender, RoutedEventArgs e)
        {
            PlayNextSong();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                mediaPlayer.Stop();
            }

            if (playlistView.SelectedIndex >= 0)
            {
                string selectedSong = playlist[playlistView.SelectedIndex];
                PlaySong(selectedSong);
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                // Pause the media
                mediaPlayer.Pause();
                isPlaying = false;
            }
            else
            {
                // Resume playback
                mediaPlayer.Play();
                isPlaying = true;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop the media
            mediaPlayer.Stop();
            isPlaying = false;
        }

        private void UpdateProgressBar()
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                progressBar.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                progressBar.Value = mediaPlayer.Position.TotalSeconds;
            }
        }


        private void SeekToPosition(double positionInSeconds)
        {
            // Seek to the specified position in the song
            mediaPlayer.Position = TimeSpan.FromSeconds(positionInSeconds);
            UpdateProgressBar();
        }

        private void MediaPlayer_MediaOpened(object? sender, EventArgs e)
        {
            // Update the progress bar when a new media is opened
            UpdateProgressBar();
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            UpdateProgressBar();
        }

        private void ProgressBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Calculate the position in the song based on the mouse click
            double totalWidth = progressBar.ActualWidth;
            double clickPosition = e.GetPosition(progressBar).X;
            double positionInSeconds = (clickPosition / totalWidth) * progressBar.Maximum;

            // Seek to the calculated position
            SeekToPosition(positionInSeconds);
        }

        private void UpdateVolume()
        {
            mediaPlayer.Volume = volumeSlider.Value;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateVolume();
        }
    }
}
