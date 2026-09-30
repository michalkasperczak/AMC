using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// F2: MALY dostepny PODGLAD ULUBIONYCH Sonos. Organizacyjnie jak istniejace
/// okno presetow WiiM i okno wyboru domu: okno NIE zna konta, klienta HTTP ani
/// magazynu - dostaje GOTOWA, swieza liste i nic nie oddaje.
///
/// TO JEST WYLACZNIE PRZEGLAD:
///  * nie ma przycisku "Odtwórz" ani przypisania presetu,
///  * Enter na liscie NIC nie robi - nie wpada do aktywacji grupy Sonos ani do
///    ogolnego odtwarzania, bo okno jest modalne i samo obsluguje klawisze,
///  * nie ma tu zadnego POST i zadnej imitacji dzialania.
/// Rzeczywiste uruchamianie ulubionego i presety to F3, z osobnym kontraktem.
///
/// Etykiety pochodza z bezpiecznego <see cref="SonosFavoritesLabels"/>:
/// identyfikator ulubionego, identyfikator uslugi ani dom NIE wyciekaja.
/// </summary>
public partial class SonosFavoritesWindow : Window
{
    private readonly ObservableCollection<FavoriteRow> _rows = [];

    internal SonosFavoritesWindow(IReadOnlyList<SonosFavorite> favorites)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        InitializeComponent();

        // KOLEJNOSC API zachowana 1:1. Zadnego sortowania i zadnego scalania
        // rownych nazw - rozne identyfikatory to rozne pozycje.
        var labels = SonosFavoritesLabels.DescribeAll(favorites);
        for (var index = 0; index < favorites.Count; index++)
        {
            _rows.Add(new FavoriteRow(labels[index]));
        }

        FavoritesList.ItemsSource = _rows;
        // PUSTY STAN mowi sie w PIERWSZEJ tresci okna, a nie udawanym wierszem.
        IntroductionText.Text = _rows.Count == 0
            ? SonosFavoritesLabels.EmptyState + " " + SonosFavoritesLabels.ViewIntroduction
            : SonosFavoritesLabels.ViewIntroduction;
        if (_rows.Count > 0) FavoritesList.SelectedIndex = 0;

        Loaded += (_, _) => FocusInitialElement();
    }

    internal int RowCountForTests => _rows.Count;

    internal IReadOnlyList<string> RowLabelsForTests => _rows.Select(row => row.Label).ToArray();

    internal string IntroductionForTests => IntroductionText.Text;

    /// <summary>
    /// Czy FOKUS naprawde jest na liscie (albo na jej wierszu). Pusty stan nie
    /// ma wiersza, wiec fokus idzie na sama liste - nadal do czytania strzalkami.
    /// </summary>
    internal bool ListHasFocusForTests =>
        FavoritesList.IsKeyboardFocusWithin || FavoritesList.IsKeyboardFocused;

    /// <summary>
    /// Czy przy tym oknie wolno oczekiwac JAKIEJKOLWIEK drogi uruchomienia.
    /// Stale false i mierzone: F2 nie ma prawa niczego odtwarzac.
    /// </summary>
    internal static bool OffersPlaybackForTests => false;

    /// <summary>
    /// ZAMKNIECIE dziala i modalnie, i przy zwyklym Show. DialogResult poza
    /// ShowDialog rzuca, wiec wyjatek jest pochlaniany i okno zamyka sie jawnie.
    /// </summary>
    private void CloseSelf()
    {
        try
        {
            DialogResult = false;
            return;
        }
        catch (InvalidOperationException)
        {
        }

        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseSelf();

    /// <summary>
    /// Escape zamyka. ENTER JEST TU SWIADOMIE POCHLONIETY na liscie: w F2 nie ma
    /// czego uruchamiac, a bez tego Enter poszedlby do domyslnego przycisku albo
    /// dalej w gore - i uzytkownik moglby uznac, ze cos zostalo wlaczone.
    /// Tab i strzalki zostaja standardowe.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            CloseSelf();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && FavoritesList.IsKeyboardFocusWithin)
        {
            // ZERO dzialania: tylko zatrzymanie klawisza.
            e.Handled = true;
        }
    }

    /// <summary>
    /// POCZATKOWY FOKUS: pierwszy wiersz listy, a gdy lista jest pusta - sama
    /// lista. Nie przycisk Zamknij, bo uzytkownik przychodzi tu czytac.
    /// </summary>
    private void FocusInitialElement()
    {
        FavoritesList.UpdateLayout();
        if (_rows.Count > 0
            && FavoritesList.ItemContainerGenerator.ContainerFromIndex(FavoritesList.SelectedIndex)
                is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }

        FavoritesList.Focus();
        Keyboard.Focus(FavoritesList);
    }

    /// <summary>
    /// WIERSZ PODGLADU: DEDYKOWANY, typowany, niesie WYLACZNIE gotowa etykiete.
    /// Nie ma tu ani identyfikatora, ani udawanego <c>Track</c>/<c>Device</c>,
    /// ani nowego rodzaju pozycji multimedialnej - wiec nie ma czego przypadkiem
    /// oddac ogolnemu odtwarzaniu.
    /// </summary>
    internal sealed class FavoriteRow(string label)
    {
        public string Label { get; } = label;

        public override string ToString() => Label;
    }
}
