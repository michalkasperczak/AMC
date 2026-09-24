using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace AccessibleMediaController.Core.Configuration;

/// <summary>
/// Tworzy odłączoną, głęboką kopię obiektów modelu konfiguracji.
///
/// DLACZEGO TO ISTNIEJE. Migawka stanu dla zapisu w tle powstaje na wątku
/// interfejsu. Wcześniej robił ją pełny obieg JSON (serializacja i ponowne
/// parsowanie). Archiwum podcastów było z tego obiegu wyłączone i kopiowane
/// ręcznie wypisanymi polami, ale pozostała część modelu — pliki lokalne,
/// katalogi Spotify i TIDAL-a, zakładki, historia — szła przez JSON i przy
/// dużej bibliotece kosztowała setki milisekund oraz dziesiątki megabajtów
/// nowych obiektów, w tym nowe kopie swoich napisów, mimo że napisy w .NET są
/// niezmienne i można je bezpiecznie współdzielić.
///
/// CO ROBI. Dla każdego typu raz buduje i zapamiętuje skompilowaną funkcję
/// kopiującą. Kopia jest głęboka dla wszystkiego, co da się zmienić: obiektów
/// ustawień, list i słowników. Napisy, liczby, wartości logiczne, wyliczenia i
/// znaczniki czasu są niezmienne, więc są przepisywane wprost.
///
/// CZEGO PILNUJE. Kopiowane są wszystkie publiczne właściwości z PUBLICZNYM
/// ustawiaczem (także `init`), odczytane z typu w czasie działania. Dodanie
/// nowego pola z publicznym ustawiaczem NIE wymaga zmian w tym pliku. Pola
/// publiczne oraz właściwości bez publicznego ustawiacza są POMIJANE — dziś
/// model ich nie ma, a strażnik testowy (CloneStateCostTests) wywala się na
/// pierwszej takiej właściwości, żeby pominięcie nie było ciche.
/// Słowniki zachowują swój komparator, więc migawka rozpoznaje klucze tak samo
/// jak stan żywy.
///
/// KONTRAKT MODELU. Ten kopiowacz obsługuje DOKŁADNIE kształty występujące w
/// aktualnym modelu konfiguracji: klasy danych z konstruktorem bezparametrowym,
/// `List&lt;T&gt;`, `Dictionary&lt;TKey, TValue&gt;` o NIEZMIENNYM kluczu oraz
/// wartości niezmienne. Każdy inny kształt — inna kolekcja, tablica, słownik o
/// mutowalnym kluczu, typ bez konstruktora bezparametrowego — jest ODRZUCANY
/// wyjątkiem przy budowie planu, więc nie ma cichej płytkiej ani pustej kopii.
///
/// CZEGO NIE OBIECUJE. Brak cykli jest ZAŁOŻENIEM O MODELU, nie chronioną w
/// czasie działania niezmiennością: graf typów aktualnego modelu jest
/// acykliczny (pilnuje tego przejście całego modelu w strażniku testowym), ale
/// gdyby powstał cykl w grafie OBIEKTÓW, rekurencja kopiowania skończyłaby się
/// przerwaniem procesu, a nie wyjątkiem do przechwycenia. Plan powstaje dla
/// typu STATYCZNEGO właściwości, więc instancja klasy pochodnej zostałaby
/// skopiowana jako klasa bazowa; dziś wszystkie klasy modelu są `sealed`.
/// </summary>
internal static class StateSnapshotCopier
{
    /// <summary>Głęboka, odłączona kopia obiektu modelu.</summary>
    public static T Copy<T>(T source) where T : class => Cloner<T>.Clone(source);

    // Wywoływane z wygenerowanego kodu. Osobna metoda zamiast odczytu pola
    // Cloner&lt;T&gt;.Clone w trakcie budowy planu: dzięki temu plany typów
    // zależnych powstają dopiero przy pierwszym użyciu, a nie w statycznym
    // konstruktorze typu nadrzędnego.
    private static TValue CloneValue<TValue>(TValue value) => Cloner<TValue>.Clone(value);

    private static List<TElement>? CloneList<TElement>(List<TElement>? source)
    {
        if (source is null) return null;
        var copy = new List<TElement>(source.Count);
        for (var i = 0; i < source.Count; i++) copy.Add(Cloner<TElement>.Clone(source[i]));
        return copy;
    }

    private static List<TElement>? CloneLeafList<TElement>(List<TElement>? source) =>
        source is null ? null : new List<TElement>(source);

    private static Dictionary<TKey, TValue>? CloneDictionary<TKey, TValue>(
        Dictionary<TKey, TValue>? source)
        where TKey : notnull
    {
        if (source is null) return null;
        // Komparator musi zostać zachowany: model używa słowników
        // nieczułych na wielkość liter, a migawka ma odpowiadać na te same
        // klucze co stan żywy.
        var copy = new Dictionary<TKey, TValue>(source.Count, source.Comparer);
        foreach (var pair in source) copy.Add(pair.Key, Cloner<TValue>.Clone(pair.Value));
        return copy;
    }

    private static Dictionary<TKey, TValue>? CloneLeafDictionary<TKey, TValue>(
        Dictionary<TKey, TValue>? source)
        where TKey : notnull =>
        source is null ? null : new Dictionary<TKey, TValue>(source, source.Comparer);

    /// <summary>
    /// Plan kopiowania dla jednego typu, budowany raz i zapamiętywany.
    /// </summary>
    private static class Cloner<T>
    {
        public static readonly Func<T, T> Clone = Build();

        private static Func<T, T> Build()
        {
            var type = typeof(T);
            if (IsImmutable(type)) return static value => value;

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var arguments = type.GetGenericArguments();
                if (definition == typeof(List<>))
                {
                    var name = IsImmutable(arguments[0]) ? nameof(CloneLeafList) : nameof(CloneList);
                    return MakeDelegate(name, arguments);
                }
                if (definition == typeof(Dictionary<,>))
                {
                    // Klucz jest przepisywany WPROST, bez kopiowania. Dla klucza
                    // mutowalnego znaczyłoby to WSPÓŁDZIELENIE obiektu klucza
                    // między stanem żywym a migawką, czyli cichą utratę
                    // odłączenia. Model używa dziś tylko kluczy string i int.
                    if (!IsImmutable(arguments[0]))
                    {
                        throw new InvalidOperationException(
                            $"Model konfiguracji zawiera słownik o mutowalnym kluczu {arguments[0].FullName} " +
                            $"({type.FullName}). Migawka przepisuje klucze wprost, więc taki klucz byłby " +
                            "współdzielony ze stanem żywym. Użyj klucza niezmiennego (np. string, int).");
                    }
                    var name = IsImmutable(arguments[1]) ? nameof(CloneLeafDictionary) : nameof(CloneDictionary);
                    return MakeDelegate(name, arguments);
                }
            }

            // Kolekcje inne niż List/Dictionary (HashSet, Stack, ObservableCollection,
            // interfejsy kolekcji) NIE mogą wpaść do ścieżki obiektu poniżej: ich
            // zawartość nie siedzi w publicznych właściwościach z ustawiaczem, więc
            // kopia wyszłaby PUSTA, bez żadnego sygnału. Tablice też tu trafiają.
            if (IsUnsupportedCollection(type))
            {
                throw new InvalidOperationException(
                    $"Model konfiguracji zawiera kolekcję {type.FullName}, której migawka nie umie " +
                    "odłączyć. Kopiowanie obsługuje List<T> oraz Dictionary<TKey, TValue> o niezmiennym " +
                    "kluczu. Kopiowanie takiej kolekcji jako obiektu dałoby pustą albo płytką migawkę, " +
                    "dlatego jest odrzucane wprost.");
            }

            if (!type.IsClass || type.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"Model konfiguracji zawiera typ {type.FullName}, którego nie można bezpiecznie " +
                    "skopiować jako odłączonej migawki. Oczekiwany jest prosty obiekt danych " +
                    "z konstruktorem bezparametrowym, lista, słownik albo wartość niezmienna.");
            }

            return BuildObjectCloner(type);
        }

        private static Func<T, T> BuildObjectCloner(Type type)
        {
            var source = Expression.Parameter(type, "source");
            var bindings = new List<MemberBinding>();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Właściwości tylko do odczytu są wyliczane z pozostałych i nie
                // mają własnego stanu do przeniesienia.
                if (property.GetMethod is null || property.SetMethod is null) continue;
                if (!property.SetMethod.IsPublic) continue;
                if (property.GetIndexParameters().Length > 0) continue;

                var value = (Expression)Expression.Property(source, property);
                if (!IsImmutable(property.PropertyType))
                {
                    var cloneValue = typeof(StateSnapshotCopier)
                        .GetMethod(nameof(CloneValue), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(property.PropertyType);
                    value = Expression.Call(cloneValue, value);
                }
                bindings.Add(Expression.Bind(property, value));
            }

            var body = Expression.MemberInit(Expression.New(type), bindings);
            var lambda = Expression.Lambda<Func<T, T>>(
                Expression.Condition(
                    Expression.ReferenceEqual(source, Expression.Constant(null, type)),
                    Expression.Constant(null, type),
                    body),
                source);
            return lambda.Compile();
        }

        private static Func<T, T> MakeDelegate(string methodName, Type[] arguments)
        {
            var method = typeof(StateSnapshotCopier)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(arguments);
            return method.CreateDelegate<Func<T, T>>();
        }
    }

    private static readonly ConcurrentDictionary<Type, bool> ImmutableCache = new();

    /// <summary>
    /// Czy ten typ jest kolekcją, której kopiowacz nie umie odłączyć. Osobne
    /// sprawdzenie, bo takie typy przeszłyby przez ścieżkę obiektu (mają
    /// konstruktor bezparametrowy) i dałyby PUSTĄ kopię bez żadnego sygnału.
    /// Typy niezmienne i obsługiwane (List, Dictionary) są rozstrzygane wcześniej.
    /// </summary>
    private static bool IsUnsupportedCollection(Type type) =>
        typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string);

    /// <summary>
    /// Czy wartość tego typu można współdzielić między stanem żywym a migawką.
    /// Napisy w .NET są niezmienne, więc ich kopiowanie było czystym kosztem.
    /// </summary>
    private static bool IsImmutable(Type type) => ImmutableCache.GetOrAdd(type, static candidate =>
    {
        var underlying = Nullable.GetUnderlyingType(candidate) ?? candidate;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid)
            || underlying == typeof(Uri)
            || underlying == typeof(Version);
    });
}
