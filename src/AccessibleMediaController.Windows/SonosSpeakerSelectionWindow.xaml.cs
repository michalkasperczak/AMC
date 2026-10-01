using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// WYBOR GLOSNIKOW dla biezacego celu sterowania (Ctrl+F5 -> "Wybierz glosniki").
///
/// Czego to okno NIE ROBI:
///  * NIE WYSYLA nic samo. Zaznaczanie i "Zaznacz wszystkie" zmieniaja TYLKO
///    zaznaczenie; Anuluj i Escape koncza droge z ZEROWA liczba POST.
///  * NIE GRA i nie zatrzymuje: zadnego Play/Stop i zadnego ponownego ladowania
///    materialu. Biezacy material i pauza zostaja.
///  * NIE ROZBIJA zestawow stereo/kina: pozycja na liscie to LOGICZNY glosnik z
///    odczytu (topology.Players), nie deviceId, nie SUB i nie satelita.
///
/// DOSTEPNOSC: wiersz to PRAWDZIWY CheckBox, wiec czytnik ekranu podaje role
/// "pole wyboru" i stan zaznaczenia na kontrolce, ktora ma fokus. Gora/Dol
/// przechodzi po polach, Spacja przelacza, Tab wychodzi do przyciskow - bez
/// liczenia kilkudziesieciu tabulacji.
/// </summary>
public partial class SonosSpeakerSelectionWindow : Window
{
    private readonly ObservableCollection<SpeakerRow> _rows = [];
    private readonly HashSet<string> _initialSelection = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="topology"/> to ODCZYTANA migawka, <paramref name="currentGroupId"/>
    /// biezacy cel. Czlonkowie aktywnej grupy sa JUZ zaznaczeni.
    /// </summary>
    internal SonosSpeakerSelectionWindow(SonosHouseholdTopology topology, string? currentGroupId)
    {
        ArgumentNullException.ThrowIfNull(topology);
        InitializeComponent();

        var current = SonosActiveGroupPolicy.Resolve(currentGroupId, topology);
        var members = new HashSet<string>(
            current?.PlayerIds ?? (IReadOnlyList<string>)[], StringComparer.Ordinal);

        // KOLEJNOSC ODCZYTU zachowana 1:1 - zadnego sortowania po nazwie.
        foreach (var player in topology.Players)
        {
            var selected = members.Contains(player.Id);
            if (selected) _initialSelection.Add(player.Id);
            var row = new SpeakerRow(
                player.Id,
                SonosSpeakerSelectionLabels.DescribePlayer(player),
                selected,
                DescribeMembership(topology, current, player.Id));
            row.PropertyChanged += Row_PropertyChanged;
            _rows.Add(row);
        }

        var parts = new List<string> { SonosSpeakerSelectionLabels.ViewIntroduction };
        if (_rows.Count == 0) parts.Add(SonosSpeakerSelectionLabels.EmptyState);
        else parts.Add(SonosSpeakerSelectionLabels.DescribeSelectionCount(_initialSelection.Count, _rows.Count));
        if (topology.Partial) parts.Add(SonosSpeakerSelectionLabels.PartialTopology);
        IntroductionText.Text = string.Join(" ", parts);

        SpeakersList.ItemsSource = _rows;
        ApplyButton.IsEnabled = _rows.Count > 0;
        SelectAllButton.IsEnabled = _rows.Count > 0;

        AutomationProperties.SetHelpText(SpeakersScroll,
            "Strzałki w górę i w dół przechodzą po polach wyboru głośników. Spacja zaznacza "
            + "i odznacza. Tab przechodzi do przycisków Zaznacz wszystkie, Zastosuj i Anuluj. "
            + "Zaznaczanie samo nic nie wysyła.");

        Loaded += (_, _) => FocusFirstCheckBox();
    }

    /// <summary>
    /// Czy uzytkownik nacisnal "Zastosuj". Wlasna flaga, bo DialogResult da sie
    /// ustawic TYLKO na oknie pokazanym przez ShowDialog.
    /// </summary>
    internal bool Applied { get; private set; }

    /// <summary>Zaznaczone identyfikatory w KOLEJNOSCI odczytu topologii.</summary>
    internal IReadOnlyList<string> SelectedPlayerIds =>
        _rows.Where(row => row.IsSelected).Select(row => row.Id).ToArray();

    /// <summary>Czy zaznaczenie ROZNI sie od stanu z otwarcia okna.</summary>
    internal bool SelectionChanged
    {
        get
        {
            var now = SelectedPlayerIds;
            return now.Count != _initialSelection.Count || now.Any(id => !_initialSelection.Contains(id));
        }
    }

    internal int RowCountForTests => _rows.Count;

    internal IReadOnlyList<string> RowNamesForTests => _rows.Select(row => row.Name).ToArray();

    internal IReadOnlyList<bool> RowCheckedForTests => _rows.Select(row => row.IsSelected).ToArray();

    internal string IntroductionForTests => IntroductionText.Text;

    internal string StatusForTests => StatusText.Text;

    internal ItemsControl ListForTests => SpeakersList;

    internal Button ApplyButtonForTests => ApplyButton;

    internal Button SelectAllButtonForTests => SelectAllButton;

    /// <summary>Opis stanu zaznaczenia dla pomiaru i dla czytnika.</summary>
    internal string SelectionSummaryForTests =>
        SonosSpeakerSelectionLabels.DescribeSelectionCount(SelectedPlayerIds.Count, _rows.Count);

    /// <summary>PRAWDZIWY CheckBox wiersza - do pomiaru roli i stanu w czytniku.</summary>
    internal CheckBox? CheckBoxForTests(int index)
    {
        if (index < 0 || index >= _rows.Count) return null;
        SpeakersList.UpdateLayout();
        var container = SpeakersList.ItemContainerGenerator.ContainerFromIndex(index);
        return container is null ? null : FindCheckBox(container);
    }

    internal void SelectAllForTests() => SelectAll();

    /// <summary>Ustawia pole wyboru wiersza - do pomiaru zamierzonego zestawu.</summary>
    internal void SetRowForTests(int index, bool selected)
    {
        if (index < 0 || index >= _rows.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _rows[index].IsSelected = selected;
    }

    internal void ApplyForTests() => Apply();

    private static CheckBox? FindCheckBox(DependencyObject root)
    {
        if (root is CheckBox box) return box;
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            if (FindCheckBox(System.Windows.Media.VisualTreeHelper.GetChild(root, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// PODPOWIEDZ wiersza: do ktorej grupy glosnik nalezy TERAZ i czy ta grupa
    /// gra. Czytnik dostaje to jako opis pola, wiec nikt nie musi zgadywac, skad
    /// glosnik zostanie zabrany.
    /// </summary>
    private static string DescribeMembership(
        SonosHouseholdTopology topology, SonosGroup? current, string playerId)
    {
        var group = topology.Groups.FirstOrDefault(candidate =>
            candidate.PlayerIds.Contains(playerId, StringComparer.Ordinal));
        if (group is null) return "Nie należy teraz do żadnej odczytanej grupy.";
        if (current is not null && string.Equals(group.Id, current.Id, StringComparison.Ordinal))
        {
            return "Należy już do bieżącego celu sterowania.";
        }

        var name = string.IsNullOrWhiteSpace(group.Name) ? "innej grupy" : "grupy " + group.Name;
        return group.PlaybackState switch
        {
            SonosPlaybackState.Playing => "Należy do " + name + ", która teraz odtwarza.",
            SonosPlaybackState.Buffering => "Należy do " + name + ", która teraz się buforuje.",
            SonosPlaybackState.Unknown => "Należy do " + name + "; Sonos nie podał jej stanu.",
            SonosPlaybackState.Paused => "Należy do " + name + ", która jest wstrzymana.",
            _ => "Należy do " + name + "."
        };
    }

    /// <summary>
    /// ZMIANA pola wyboru: TYLKO zaznaczenie. Status NIE jest tu oglaszany
    /// ponownie - czytnik sam mowi nowy stan pola, ktore ma fokus, a drugi
    /// komunikat byl by DUBLEM tego samego.
    /// </summary>
    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SpeakerRow.IsSelected)) return;
        StatusText.Text = SelectionSummaryForTests;
    }

    /// <summary>
    /// "Zaznacz wszystkie" / "Odznacz wszystkie": zmienia WYLACZNIE zaznaczenie,
    /// zero POST. Jeden komunikat o nowym stanie, bo zmienilo sie wiele pol.
    /// </summary>
    private void SelectAll()
    {
        if (_rows.Count == 0) return;
        var selectAll = _rows.Any(row => !row.IsSelected);
        foreach (var row in _rows) row.IsSelected = selectAll;
        StatusText.Announce(selectAll
            ? "Zaznaczono wszystkie głośniki. " + SelectionSummaryForTests + ". Nic nie wysłano."
            : "Odznaczono wszystkie głośniki. Nic nie wysłano.");
    }

    /// <summary>
    /// "Zastosuj": JEDNA intencja. Okno samo NIE wysyla - oddaje zaznaczenie
    /// wlascicielowi, ktory ma bramke polecen, swiezy odczyt i potwierdzenie.
    /// </summary>
    private void Apply()
    {
        if (SelectedPlayerIds.Count == 0)
        {
            StatusText.Announce(SonosSpeakerSelectionLabels.NothingSelected);
            return;
        }

        if (!SelectionChanged)
        {
            // NIEZMIENIONE zaznaczenie: zero POST i jawne zdanie dlaczego.
            StatusText.Announce(SonosSpeakerSelectionLabels.UnchangedSelection);
            return;
        }

        Applied = true;
        CloseSelf(true);
    }

    private void Cancel()
    {
        // ZERO POST: nie oddajemy intencji.
        Applied = false;
        CloseSelf(false);
    }

    private void CloseSelf(bool result)
    {
        try
        {
            DialogResult = result;
            return;
        }
        catch (InvalidOperationException)
        {
        }

        Close();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SelectAll();

    private void Apply_Click(object sender, RoutedEventArgs e) => Apply();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Cancel();

    /// <summary>
    /// Escape anuluje, a Gora/Dol chodzi po polach wyboru. Spacje obsluguje sam
    /// CheckBox, wiec nie dubluje sie tutaj.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
            return;
        }

        if (e.Key is not (Key.Down or Key.Up)) return;
        if (Keyboard.FocusedElement is not CheckBox focused) return;

        var index = _rows.ToList().FindIndex(row => ReferenceEquals(row, focused.DataContext));
        if (index < 0) return;

        var next = e.Key == Key.Down ? index + 1 : index - 1;
        if (next < 0 || next >= _rows.Count)
        {
            e.Handled = true;
            return;
        }

        if (CheckBoxForTests(next) is { } target)
        {
            target.BringIntoView();
            target.Focus();
            Keyboard.Focus(target);
        }

        e.Handled = true;
    }

    private void FocusFirstCheckBox()
    {
        SpeakersList.UpdateLayout();
        if (CheckBoxForTests(0) is { } first)
        {
            first.Focus();
            Keyboard.Focus(first);
            return;
        }

        ApplyButton.Focus();
    }

    /// <summary>
    /// WIERSZ: identyfikator WEWNETRZNIE, PELNA nazwa na widoku. Stan pola
    /// wyboru jest wiazany dwukierunkowo, wiec Spacja na CheckBoxie zmienia
    /// dokladnie ten model, ktory czyta "Zastosuj".
    /// </summary>
    internal sealed class SpeakerRow(string id, string name, bool selected, string helpText)
        : INotifyPropertyChanged
    {
        private bool _isSelected = selected;

        internal string Id { get; } = id;

        public string Name { get; } = name;

        public string HelpText { get; } = helpText;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => Name;
    }
}
