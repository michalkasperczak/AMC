using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// BIBLIOTEKA MATERIALU SONOSA (Ctrl+L): kategorie "Ulubione Sonos" i
/// "Playlisty Sonos".
///
/// Dlaczego OKNO, a nie wiersze w liscie sesji: lista sesji Sonos jest MODELEM
/// STEROWANIA (glosniki i grupy z topologii) i jest cyklicznie publikowana przez
/// <c>ApplySonosGroupRows</c> z istniejacego timera odczytow. Kategorie wrzucone
/// do TEJ listy albo by z niej wylecialy przy najblizszym odswiezeniu topologii,
/// albo przestawilyby zaznaczenie w trakcie czytania przez czytnik ekranu.
/// Osobne okno jest odporne na tamten timer Z DEFINICJI: nie uczestniczy w
/// publikacji topologii. Dzieki temu kategorie nie gina, a lista glosnikow nie
/// zamienia sie w pozorna biblioteke.
///
/// KATEGORIA NIE JEST MATERIALEM: te wiersze NIE WCHODZA do Session.Items, wiec
/// ani ogolny tor odtwarzania, ani "dodaj do ulubionych", ani zapis presetu nie
/// maja czego zlapac.
///
/// ZERO POST: otwarcie okna, ruch strzalkami i Tab nie wysylaja ani jednego
/// zapytania. Enter / "Otwórz kategorię" jedynie OTWIERA liste danej kategorii.
/// </summary>
public partial class SonosLibraryWindow : Window
{
    private readonly IReadOnlyList<SonosLibraryPresentation.SonosLibraryCategoryRow> _categories;
    private readonly Action<SonosLibraryPresentation.SonosLibraryCategoryRow>? _open;
    private bool _closed;

    /// <summary>
    /// <paramref name="open"/> jest OPCJONALNE. Bez niego okno jest czystym
    /// podgladem kategorii, a przycisk otwarcia jest WYLACZONY - zamiast martwego
    /// przycisku, ktory udaje dzialanie.
    /// </summary>
    internal SonosLibraryWindow(
        Action<SonosLibraryPresentation.SonosLibraryCategoryRow>? open = null)
    {
        InitializeComponent();

        _categories = SonosLibraryPresentation.DescribeCategories();
        _open = open;

        IntroductionText.Text = SonosLibraryPresentation.ViewIntroduction;
        CategoryList.ItemsSource = _categories;

        // Zaznaczenie na pierwszej kategorii: czytnik ma od razu co czytac, a
        // Enter ma jednoznaczny cel. Kolejnosc kategorii jest STALA, wiec to nie
        // jest zgadywanie.
        if (_categories.Count > 0) CategoryList.SelectedIndex = 0;

        OpenButton.IsEnabled = _open is not null;
        if (_open is null)
        {
            AutomationProperties.SetHelpText(
                OpenButton,
                "To zaplecze Sonos nie udostępnia otwierania kategorii Biblioteki.");
        }

        AutomationProperties.SetHelpText(CategoryList,
            "Strzałki czytają kolejne kategorie materiału z konta Sonos. Enter albo przycisk "
            + "Otwórz kategorię otwiera jej listę. Głośniki i grupy to nie materiał - "
            + "cel sterowania wybierasz skrótem Control F5.");

        CategoryList.SelectionChanged += (_, _) => DescribeSelection();
        Closed += (_, _) => _closed = true;
        Loaded += (_, _) =>
        {
            FocusInitialElement();
            DescribeSelection();
        };
    }

    /// <summary>Czy okno nadal jest ZYWYM adresatem statusu.</summary>
    internal bool IsLiveOwnerTarget => !_closed && IsVisible;

    internal int CategoryCountForTests => _categories.Count;

    internal IReadOnlyList<string> CategoryNamesForTests =>
        _categories.Select(category => category.Name).ToArray();

    internal string IntroductionForTests => IntroductionText.Text;

    internal string StatusForTests => StatusText.Text;

    internal bool OpenEnabledForTests => OpenButton.IsEnabled;

    internal string? HighlightedCategoryIdForTests =>
        (CategoryList.SelectedItem as SonosLibraryPresentation.SonosLibraryCategoryRow)?.CategoryId;

    internal bool ListHasFocusForTests =>
        CategoryList.IsKeyboardFocusWithin || CategoryList.IsKeyboardFocused;

    /// <summary>Lista kategorii dla POMIARU drogi klawiatury (prawdziwe zdarzenia).</summary>
    internal ListBox ListForTests => CategoryList;

    /// <summary>PRODUKCYJNA droga Enter / "Otwórz kategorię" - ten sam kod.</summary>
    internal void OpenSelectedForTests() => OpenSelected();

    internal void SelectRowForTests(int index)
    {
        if (index < 0 || index >= _categories.Count) throw new ArgumentOutOfRangeException(nameof(index));
        CategoryList.SelectedIndex = index;
        CategoryList.UpdateLayout();
        FocusSelectedRow();
    }

    /// <summary>
    /// STATUS W TYM oknie. Gdy okno nie zyje, komunikat jest CICHY - nie przenosi
    /// sie do okna glownego i nie wchodzi w nowo otwarte okno.
    /// </summary>
    internal void AnnounceForOwner(string message)
    {
        if (_closed) return;
        if (string.IsNullOrWhiteSpace(message)) return;
        StatusText.Announce(message);
    }

    /// <summary>
    /// Opis zaznaczonej kategorii. SAM RUCH po liscie nie wysyla zapytan - to
    /// wylacznie tekst z Core.
    /// </summary>
    private void DescribeSelection()
    {
        if (CategoryList.SelectedItem
            is not SonosLibraryPresentation.SonosLibraryCategoryRow row) return;
        StatusText.Text = row.Hint;
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Escape i Enter obsluguje WLASNY kod okna, wiec dzialaja tak samo modalnie
    /// i przy zwyklym Show. Autorepeat nie dubluje otwarcia.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || !CategoryList.IsKeyboardFocusWithin) return;

        e.Handled = true;
        if (e.IsRepeat) return;
        OpenSelected();
    }

    /// <summary>
    /// OTWARCIE kategorii. Rozpoznanie idzie po IDENTYFIKATORZE wiersza, NIGDY po
    /// polskiej nazwie wyswietlanej uzytkownikowi.
    ///
    /// Okno ZAMYKA SIE PRZED wywolaniem akcji, bo brama prezentacji w oknie
    /// glownym odmawia, gdy jakiekolwiek okno potomne jest widoczne. Zamkniecie
    /// oddaje tez fokus wlascicielowi, wiec Ctrl+L -> Enter -> lista pozycji jest
    /// jedna ciagla droga klawiatury.
    /// </summary>
    private void OpenSelected()
    {
        if (_open is null)
        {
            StatusText.Announce("To zaplecze Sonos nie udostępnia otwierania kategorii Biblioteki.");
            return;
        }

        if (CategoryList.SelectedItem
            is not SonosLibraryPresentation.SonosLibraryCategoryRow row)
        {
            StatusText.Announce("Najpierw wybierz kategorię na liście.");
            return;
        }

        var open = _open;
        Close();
        open(row);
    }

    private void FocusInitialElement()
    {
        CategoryList.UpdateLayout();
        if (_categories.Count > 0 && FocusSelectedRow()) return;

        CategoryList.Focus();
        Keyboard.Focus(CategoryList);
    }

    private bool FocusSelectedRow()
    {
        if (CategoryList.ItemContainerGenerator.ContainerFromIndex(CategoryList.SelectedIndex)
            is not ListBoxItem item)
        {
            return false;
        }

        item.Focus();
        Keyboard.Focus(item);
        return true;
    }
}
