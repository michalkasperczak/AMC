using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows.Services;

namespace SonosCredentialStoreHarness;

/// <summary>
/// Test trwalego magazynu poswiadczen Sonos. Uruchamiany NATYWNIE na Windows,
/// wiec DPAPI biezacego uzytkownika jest PRAWDZIWE - nie ma tu atrapy
/// szyfrowania ani atrapy magazynu.
///
/// Zasady tego harnessu:
///   * wylacznie SYNTETYCZNE wartosci i wylacznie swiezy, losowy katalog w
///     Windows TEMP; zaden istniejacy plik danych nie jest czytany ani ruszany,
///   * zaden komunikat - takze przy porazce - nie wypisuje tokenu, scope ani
///     origin; raportujemy nazwy kontroli, nie wartosci,
///   * proces potomny dostaje tylko SCIEZKE i nazwe scenariusza; oczekiwane
///     wartosci sa stalymi w tym pliku, wiec zaden token nie idzie przez
///     argumenty ani zmienne srodowiskowe,
///   * kod wyjscia 1 dla DOWOLNEJ porazki,
///   * wlasne bloby i katalogi sprzatane w finally; nic obcego nie jest usuwane.
/// </summary>
internal static class Program
{
    private const string BrokerOrigin = "https://sonos-auth.example.invalid/";
    private const string OtherBrokerOrigin = "https://inny-broker.example.invalid/";

    // Stale, SYNTETYCZNE wartosci bazowego rekordu. Dzielone z procesem
    // potomnym jako stale kompilacji, nie jako argumenty.
    private const string BaseAccessToken = "AT-podstawowy";
    private const string BaseRefreshToken = "RT-podstawowy";
    private const string BaseScope = "playback-control-all";
    private const int BaseExpiresIn = 3600;
    private static readonly DateTimeOffset BaseReceivedAt =
        new(2026, 9, 27, 11, 22, 33, TimeSpan.Zero);

    // Entropia domeny magazynu. Nie jest sekretem - potrzebna, by sfalszowac
    // plik z NIEOBSLUGIWANA wersja formatu, ktorego magazyn ma nie przyjac.
    private static readonly byte[] DomainEntropy =
        Encoding.UTF8.GetBytes("AccessibleMediaController/Sonos/credentials/v1");

    private static int _failures;

    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("BLAD: harness wymaga Windows (DPAPI biezacego uzytkownika).");
            return 1;
        }

        if (args.Length == 2 && args[0] == "child-read")
        {
            return ChildExpectsBaseRecord(args[1]);
        }

        if (args.Length == 2 && args[0] == "child-missing")
        {
            return ChildExpectsMissing(args[1]);
        }

        if (args.Length == 2 && args[0] == "child-read-rotated")
        {
            return ChildExpectsRotatedRefresh(args[1]);
        }

        var root = Path.Combine(Path.GetTempPath(), "amc-sonos-cred-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Run("1.roundtrip-caly-zestaw", RoundtripPreservesWholeSet, root);
            Run("2.ciphertext-bez-markerow", CiphertextHasNoPlaintextMarkers, root);
            Run("3.literal-unicode-i-spacje", OpaqueLiteralsPreserved, root);
            Run("4.brak-rt-waznosc-nieznana", MissingRefreshAndUnknownExpiry, root);
            Run("5.nowy-proces-czyta-utc", NewProcessReadsPreservedUtc, root);
            Run("6.rotacja-rt", RotationPersistsNewRefresh, root);
            Run("7.brak-pliku", MissingFileIsNotFailure, root);
            Run("8.uszkodzony-nie-kasuje", CorruptedCiphertextDoesNotDeleteData, root);
            Run("9.zla-wersja-nie-kasuje", UnsupportedFormatDoesNotDeleteData, root);
            Run("10.inny-broker-odrzuca", ForeignBrokerRejected, root);
            Run("11.zle-wejscie-nie-nadpisuje", InvalidInputDoesNotOverwrite, root);
            Run("12.blad-zapisu-zachowuje", WriteFailureKeepsPrevious, root);
            Run("13.delete-idempotentny", ExplicitDeleteThenMissingInNewProcess, root);
            Run("14.limit-rozmiaru", OversizedFileRejected, root);
            Run("15.tostring-bez-sekretow", ToStringHidesSecrets, root);
        }
        catch (Exception ex)
        {
            _failures++;
            Console.Error.WriteLine("WYJATEK " + ex.GetType().Name);
        }
        finally
        {
            TryCleanup(root);
        }

        Console.WriteLine(_failures == 0
            ? "WYNIK: wszystkie kontrole zaliczone"
            : "WYNIK: porazek " + _failures.ToString(CultureInfo.InvariantCulture));
        return _failures == 0 ? 0 : 1;
    }

    private static void Run(string scenario, Action<string> body, string root)
    {
        var path = FreshPath(root);
        try
        {
            body(path);
        }
        catch (Exception ex)
        {
            Fail(scenario, "wyjatek " + ex.GetType().Name);
        }
    }

    /// <summary>Swieza, losowa sciezka - zawsze JAWNIE przekazana magazynowi.</summary>
    private static string FreshPath(string root) =>
        Path.Combine(root, Guid.NewGuid().ToString("N"), "sonos.bin");

    private static SonosLoginBrokerConfiguration Broker(string origin)
    {
        if (!SonosLoginBrokerConfiguration.TryCreate(origin, out var broker) || broker is null)
        {
            throw new InvalidOperationException("konfiguracja brokera nie powstala");
        }

        return broker;
    }

    private static SonosStoredCredentials BaseRecord(SonosLoginBrokerConfiguration broker) =>
        new(
            broker.Origin.AbsoluteUri,
            new SonosTokens(BaseAccessToken, "Bearer", BaseExpiresIn, BaseRefreshToken, BaseScope),
            BaseReceivedAt);

    // 1. Pelny roundtrip: caly zestaw tokenow, UTC otrzymania i wyliczona waznosc.
    private static void RoundtripPreservesWholeSet(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);

        // Konstruktor nie czyta zapisanych kont - przed zapisem magazyn jest pusty.
        Check("1.przed-zapisem-brak", store.Read().Status == SonosCredentialReadStatus.Missing);

        Check("1.zapis-ok", store.Write(BaseRecord(broker)).Succeeded);
        Check("1.plik-istnieje", File.Exists(path));
        Check("1.brak-bak", !File.Exists(path + ".bak"));
        Check("1.brak-tempow", CountFiles(Path.GetDirectoryName(path)!) == 1);

        var read = store.Read();
        Check("1.odczyt-ok", read.Succeeded);
        if (read.Credentials is null)
        {
            Fail("1.odczyt-rekord", "brak rekordu po zapisie");
            return;
        }

        var got = read.Credentials;
        Check("1.access", string.Equals(got.Tokens.AccessToken, BaseAccessToken, StringComparison.Ordinal));
        Check("1.refresh", string.Equals(got.Tokens.RefreshToken, BaseRefreshToken, StringComparison.Ordinal));
        Check("1.scope", string.Equals(got.Tokens.Scope, BaseScope, StringComparison.Ordinal));
        Check("1.typ-bearer", string.Equals(got.Tokens.TokenType, "Bearer", StringComparison.Ordinal));
        Check("1.expires-in", got.Tokens.ExpiresInSeconds == BaseExpiresIn);
        Check("1.utc", got.ReceivedAtUtc == BaseReceivedAt);
        Check("1.waznosc-znana", got.IsExpiryKnown);
        Check("1.waznosc-wyliczona", got.ExpiresAtUtc == BaseReceivedAt.AddSeconds(BaseExpiresIn));
        Check("1.format-jawny", got.FormatVersion == SonosStoredCredentials.CurrentFormatVersion);
        Check("1.origin", string.Equals(got.BrokerOrigin, broker.Origin.AbsoluteUri, StringComparison.Ordinal));
    }

    // 2. Plik na dysku nie moze zawierac plaintextu w ZADNYM oczywistym kodowaniu.
    private static void CiphertextHasNoPlaintextMarkers(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);
        Check("2.zapis-ok", store.Write(BaseRecord(broker)).Succeeded);

        var bytes = File.ReadAllBytes(path);
        foreach (var (label, marker) in new[]
                 {
                     ("access", BaseAccessToken),
                     ("refresh", BaseRefreshToken),
                     ("scope", BaseScope),
                     ("origin", "sonos-auth.example.invalid"),
                     ("pole-json", "access_token")
                 })
        {
            Check("2.utf8-bez-" + label, !Contains(bytes, Encoding.UTF8.GetBytes(marker)));
            Check("2.utf16-bez-" + label, !Contains(bytes, Encoding.Unicode.GetBytes(marker)));
        }
    }

    // 3. Wartosci NIEPRZEZROCZYSTE: spacje, znaki poza ASCII i wielkosc liter
    //    wracaja bit w bit - zero trim, zero normalizacji.
    private static void OpaqueLiteralsPreserved(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);

        const string access = "  AT \u00e9\u0141\u017c \u65e5\u672c\u8a9e  ";
        const string refresh = " RT \u00c5\u00c4\u00d6 \u0101\u0113 ";
        const string scope = "  playback-control-all  household-config  ";

        var record = new SonosStoredCredentials(
            broker.Origin.AbsoluteUri,
            new SonosTokens(access, "Bearer", null, refresh, scope),
            BaseReceivedAt);
        Check("3.zapis-ok", store.Write(record).Succeeded);

        var read = store.Read();
        if (!read.Succeeded || read.Credentials is null)
        {
            Fail("3.odczyt", "rekord nie wrocil");
            return;
        }

        Check("3.access-literal", string.Equals(read.Credentials.Tokens.AccessToken, access, StringComparison.Ordinal));
        Check("3.refresh-literal", string.Equals(read.Credentials.Tokens.RefreshToken, refresh, StringComparison.Ordinal));
        Check("3.scope-literal", string.Equals(read.Credentials.Tokens.Scope, scope, StringComparison.Ordinal));

        // Zle Unicode (niesparowany surogat) jest ODRZUCANE, bez wyjatku z trescia.
        var badUnicode = new SonosStoredCredentials(
            broker.Origin.AbsoluteUri,
            new SonosTokens("AT-" + '\ud800' + "-zly", "Bearer", null, null, null),
            BaseReceivedAt);
        var rejected = store.Write(badUnicode);
        Check("3.zle-unicode-odrzucone", rejected.Status == SonosCredentialWriteStatus.InvalidRecord);

        // Odrzucony zapis NIE ruszyl poprzedniego rekordu.
        var after = store.Read();
        Check("3.poprzedni-caly",
            after.Succeeded
            && after.Credentials is not null
            && string.Equals(after.Credentials.Tokens.AccessToken, access, StringComparison.Ordinal));
    }

    // 4. PIERWSZY login moze nie dac RT, a broker moze nie podac expires_in.
    //    Oba stany zapisujemy jak sa: bez falszywego RT i bez wymyslonego TTL.
    private static void MissingRefreshAndUnknownExpiry(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);

        var record = new SonosStoredCredentials(
            broker.Origin.AbsoluteUri,
            new SonosTokens("AT-bez-rt", "Bearer", null, null, null),
            BaseReceivedAt);
        Check("4.zapis-ok", store.Write(record).Succeeded);

        var read = store.Read();
        if (!read.Succeeded || read.Credentials is null)
        {
            Fail("4.odczyt", "rekord nie wrocil");
            return;
        }

        Check("4.rt-nadal-brak", read.Credentials.Tokens.RefreshToken is null);
        Check("4.has-refresh-false", !read.Credentials.HasRefreshToken);
        Check("4.waznosc-nieznana", !read.Credentials.IsExpiryKnown);
        Check("4.brak-wyliczonej-waznosci", read.Credentials.ExpiresAtUtc is null);
        Check("4.scope-null", read.Credentials.Tokens.Scope is null);
        Check("4.utc-zachowany", read.Credentials.ReceivedAtUtc == BaseReceivedAt);
    }

    // 5. NOWY PROCES Windows odczytuje zachowany moment UTC i caly zestaw.
    private static void NewProcessReadsPreservedUtc(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);
        Check("5.zapis-ok", store.Write(BaseRecord(broker)).Succeeded);

        var exit = RunChild("child-read", path);
        Check("5.nowy-proces-odczytal", exit == 0);
    }

    // 6. Rotacja tokenu odswiezania jest UTRWALANA i widoczna w nowym procesie.
    private static void RotationPersistsNewRefresh(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);
        Check("6.zapis-pierwszy", store.Write(BaseRecord(broker)).Succeeded);

        var rotatedAt = BaseReceivedAt.AddSeconds(1800);
        var rotated = new SonosStoredCredentials(
            broker.Origin.AbsoluteUri,
            new SonosTokens(BaseAccessToken + "-2", "Bearer", 7200, BaseRefreshToken + "-2", BaseScope),
            rotatedAt);
        Check("6.zapis-rotacji", store.Write(rotated).Succeeded);
        Check("6.brak-bak-po-podmianie", !File.Exists(path + ".bak"));
        Check("6.brak-tempow-po-podmianie", CountFiles(Path.GetDirectoryName(path)!) == 1);

        var read = store.Read();
        if (!read.Succeeded || read.Credentials is null)
        {
            Fail("6.odczyt", "rekord nie wrocil");
            return;
        }

        Check("6.nowy-rt",
            string.Equals(read.Credentials.Tokens.RefreshToken, BaseRefreshToken + "-2", StringComparison.Ordinal));
        Check("6.nowy-utc", read.Credentials.ReceivedAtUtc == rotatedAt);
        Check("6.nowa-waznosc", read.Credentials.ExpiresAtUtc == rotatedAt.AddSeconds(7200));
        Check("6.nowy-proces-widzi-rotacje", RunChild("child-read-rotated", path) == 0);
    }

    // 7. Brak pliku to Missing, nie blad i nie wyjatek.
    private static void MissingFileIsNotFailure(string path)
    {
        var store = new SonosDpapiCredentialStore(Broker(BrokerOrigin), path);
        var read = store.Read();
        Check("7.status-missing", read.Status == SonosCredentialReadStatus.Missing);
        Check("7.brak-rekordu", read.Credentials is null);
        Check("7.nie-utworzono-pliku", !File.Exists(path));
        Check("7.komunikat-bez-sciezki", !read.Message.Contains(path, StringComparison.OrdinalIgnoreCase));
    }

    // 8. Uszkodzony ciphertext to Invalid, a plik ZOSTAJE nietkniety.
    private static void CorruptedCiphertextDoesNotDeleteData(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);
        Check("8.zapis-ok", store.Write(BaseRecord(broker)).Succeeded);

        var original = File.ReadAllBytes(path);
        var corrupted = (byte[])original.Clone();
        corrupted[corrupted.Length / 2] ^= 0xFF;
        File.WriteAllBytes(path, corrupted);

        var read = store.Read();
        Check("8.status-invalid", read.Status == SonosCredentialReadStatus.Invalid);
        Check("8.plik-nie-skasowany", File.Exists(path));
        Check("8.bajty-nietkniete", Contains(File.ReadAllBytes(path), corrupted) && File.ReadAllBytes(path).Length == corrupted.Length);

        // Po przywroceniu bajtow dane sa nadal dostepne - odczyt ich nie zniszczyl.
        File.WriteAllBytes(path, original);
        Check("8.dane-odzyskane", store.Read().Succeeded);
    }

    // 9. Poprawnie zaszyfrowany, ale NIEOBSLUGIWANY format to Invalid bez kasowania.
    private static void UnsupportedFormatDoesNotDeleteData(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);

        var forged = "{\"format_version\":2,\"broker_origin\":\""
                     + broker.Origin.AbsoluteUri
                     + "\",\"received_at_utc\":\""
                     + BaseReceivedAt.ToString("O", CultureInfo.InvariantCulture)
                     + "\",\"access_token\":\"AT-z-przyszlosci\",\"token_type\":\"Bearer\"}";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Protect(Encoding.UTF8.GetBytes(forged)));

        var read = store.Read();
        Check("9.status-invalid", read.Status == SonosCredentialReadStatus.Invalid);
        Check("9.plik-nie-skasowany", File.Exists(path));

        // Nieprzewidziane pole w ZNANEJ wersji tez jest Invalid, nie cichym sukcesem.
        var extra = "{\"format_version\":1,\"broker_origin\":\""
                    + broker.Origin.AbsoluteUri
                    + "\",\"received_at_utc\":\""
                    + BaseReceivedAt.ToString("O", CultureInfo.InvariantCulture)
                    + "\",\"access_token\":\"AT-obcy\",\"token_type\":\"Bearer\",\"nieznane_pole\":1}";
        File.WriteAllBytes(path, Protect(Encoding.UTF8.GetBytes(extra)));
        Check("9.nieznane-pole-invalid", store.Read().Status == SonosCredentialReadStatus.Invalid);
        Check("9.plik-nadal-jest", File.Exists(path));
    }

    // 10. Rekord innego skonfigurowanego brokera jest ODRZUCANY i NIE przelacza adresu.
    private static void ForeignBrokerRejected(string path)
    {
        var owner = Broker(BrokerOrigin);
        var other = Broker(OtherBrokerOrigin);
        Check("10.zapis-ok", new SonosDpapiCredentialStore(owner, path).Write(BaseRecord(owner)).Succeeded);

        var readerWithOtherConfig = new SonosDpapiCredentialStore(other, path);
        var read = readerWithOtherConfig.Read();
        Check("10.status-mismatch", read.Status == SonosCredentialReadStatus.BrokerMismatch);
        Check("10.brak-rekordu", read.Credentials is null);
        Check("10.komunikat-bez-origin",
            !read.Message.Contains("example.invalid", StringComparison.OrdinalIgnoreCase));
        Check("10.plik-nie-skasowany", File.Exists(path));
        // Wlasciwa konfiguracja nadal czyta swoj rekord.
        Check("10.wlasciciel-czyta", new SonosDpapiCredentialStore(owner, path).Read().Succeeded);
    }

    // 11. Rekord niezgodny z polityka NIE nadpisuje poprzedniego zapisu.
    private static void InvalidInputDoesNotOverwrite(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);
        Check("11.zapis-ok", store.Write(BaseRecord(broker)).Succeeded);
        var before = File.ReadAllBytes(path);

        // RT niezgodny z ISTNIEJACA SonosRefreshTokenPolicy (przekroczony limit).
        var tooLongRefresh = new string('r', SonosRefreshTokenPolicy.MaxDecodedBytes + 1);
        var bad = new SonosStoredCredentials(
            broker.Origin.AbsoluteUri,
            new SonosTokens(BaseAccessToken, "Bearer", BaseExpiresIn, tooLongRefresh, BaseScope),
            BaseReceivedAt);
        Check("11.rt-odrzucony", store.Write(bad).Status == SonosCredentialWriteStatus.InvalidRecord);

        // expires_in <= 0 rowniez jest niespojne.
        var badExpiry = new SonosStoredCredentials(
            broker.Origin.AbsoluteUri,
            new SonosTokens(BaseAccessToken, "Bearer", 0, BaseRefreshToken, BaseScope),
            BaseReceivedAt);
        Check("11.zla-waznosc-odrzucona", store.Write(badExpiry).Status == SonosCredentialWriteStatus.InvalidRecord);

        Check("11.bajty-bez-zmian", ByteEquals(File.ReadAllBytes(path), before));
        var read = store.Read();
        Check("11.stary-rt-caly",
            read.Succeeded
            && read.Credentials is not null
            && string.Equals(read.Credentials.Tokens.RefreshToken, BaseRefreshToken, StringComparison.Ordinal));
    }

    // 12. PRAWDZIWA blokada pliku: zapis zwraca WriteFailure, a stary plik zyje.
    private static void WriteFailureKeepsPrevious(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);
        Check("12.zapis-ok", store.Write(BaseRecord(broker)).Succeeded);
        var before = File.ReadAllBytes(path);

        var rotated = new SonosStoredCredentials(
            broker.Origin.AbsoluteUri,
            new SonosTokens(BaseAccessToken + "-nowy", "Bearer", 7200, BaseRefreshToken + "-nowy", BaseScope),
            BaseReceivedAt.AddSeconds(60));

        SonosCredentialWriteOutcome blocked;
        using (var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            blocked = store.Write(rotated);
        }

        Check("12.status-writefailure", blocked.Status == SonosCredentialWriteStatus.WriteFailure);
        Check("12.komunikat-bez-sciezki", !blocked.Message.Contains(path, StringComparison.OrdinalIgnoreCase));
        Check("12.stary-plik-zyje", File.Exists(path));
        Check("12.stare-bajty", ByteEquals(File.ReadAllBytes(path), before));
        Check("12.brak-porzuconych-tempow", CountFiles(Path.GetDirectoryName(path)!) == 1);

        var read = store.Read();
        Check("12.stary-rekord-czytelny",
            read.Succeeded
            && read.Credentials is not null
            && string.Equals(read.Credentials.Tokens.RefreshToken, BaseRefreshToken, StringComparison.Ordinal));
    }

    // 13. JAWNE Delete jest idempotentne, a nowy proces widzi Missing.
    private static void ExplicitDeleteThenMissingInNewProcess(string path)
    {
        var broker = Broker(BrokerOrigin);
        var store = new SonosDpapiCredentialStore(broker, path);
        Check("13.zapis-ok", store.Write(BaseRecord(broker)).Succeeded);

        Check("13.delete-pierwszy", store.Delete());
        Check("13.plik-znikl", !File.Exists(path));
        Check("13.delete-powtorzony", store.Delete());
        Check("13.odczyt-missing", store.Read().Status == SonosCredentialReadStatus.Missing);
        Check("13.nowy-proces-missing", RunChild("child-missing", path) == 0);
    }

    // 14. Plik ponad NASZ limit jest odrzucany po kontroli rozmiaru, bez wyjatku.
    private static void OversizedFileRejected(string path)
    {
        var store = new SonosDpapiCredentialStore(Broker(BrokerOrigin), path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var oversized = new byte[SonosCredentialPolicy.MaxEncryptedFileBytes + 1];
        Random.Shared.NextBytes(oversized);
        File.WriteAllBytes(path, oversized);

        var read = store.Read();
        Check("14.status-invalid", read.Status == SonosCredentialReadStatus.Invalid);
        Check("14.plik-nie-skasowany", File.Exists(path));
        Check("14.rozmiar-bez-zmian", new FileInfo(path).Length == oversized.Length);

        // Pusty plik rowniez nie jest cichym sukcesem.
        File.WriteAllBytes(path, Array.Empty<byte>());
        Check("14.pusty-invalid", store.Read().Status == SonosCredentialReadStatus.Invalid);
    }

    // 15. ToString modeli nie wypisuje tokenow, scope ani origin ze zrodla danych.
    private static void ToStringHidesSecrets(string path)
    {
        var broker = Broker(BrokerOrigin);
        var record = BaseRecord(broker);
        foreach (var text in new[]
                 {
                     record.ToString(),
                     record.Tokens.ToString(),
                     SonosCredentialReadOutcome.Ok(record).ToString(),
                     SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.BrokerMismatch).ToString(),
                     SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure).ToString()
                 })
        {
            Check("15.bez-access", !text.Contains(BaseAccessToken, StringComparison.Ordinal));
            Check("15.bez-refresh", !text.Contains(BaseRefreshToken, StringComparison.Ordinal));
            Check("15.bez-scope", !text.Contains(BaseScope, StringComparison.Ordinal));
            Check("15.bez-origin", !text.Contains("example.invalid", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---- proces potomny: te same stale, wlasny wynik ----

    private static int ChildExpectsBaseRecord(string path)
    {
        var read = new SonosDpapiCredentialStore(Broker(BrokerOrigin), path).Read();
        if (!read.Succeeded || read.Credentials is null)
        {
            Console.WriteLine("POTOMNY BLAD: rekord nieodczytany, status " + read.Status);
            return 1;
        }

        var got = read.Credentials;
        var ok = string.Equals(got.Tokens.AccessToken, BaseAccessToken, StringComparison.Ordinal)
                 && string.Equals(got.Tokens.RefreshToken, BaseRefreshToken, StringComparison.Ordinal)
                 && string.Equals(got.Tokens.Scope, BaseScope, StringComparison.Ordinal)
                 && got.Tokens.ExpiresInSeconds == BaseExpiresIn
                 && got.ReceivedAtUtc == BaseReceivedAt
                 && got.ReceivedAtUtc.Offset == TimeSpan.Zero
                 && got.ExpiresAtUtc == BaseReceivedAt.AddSeconds(BaseExpiresIn);
        Console.WriteLine(ok ? "POTOMNY OK: zestaw i UTC zgodne" : "POTOMNY BLAD: zestaw lub UTC niezgodne");
        return ok ? 0 : 1;
    }

    private static int ChildExpectsRotatedRefresh(string path)
    {
        var read = new SonosDpapiCredentialStore(Broker(BrokerOrigin), path).Read();
        if (!read.Succeeded || read.Credentials is null)
        {
            Console.WriteLine("POTOMNY BLAD: rekord nieodczytany, status " + read.Status);
            return 1;
        }

        var rotatedAt = BaseReceivedAt.AddSeconds(1800);
        var ok = string.Equals(read.Credentials.Tokens.RefreshToken, BaseRefreshToken + "-2", StringComparison.Ordinal)
                 && read.Credentials.ReceivedAtUtc == rotatedAt
                 && read.Credentials.ExpiresAtUtc == rotatedAt.AddSeconds(7200);
        Console.WriteLine(ok ? "POTOMNY OK: rotacja zachowana" : "POTOMNY BLAD: rotacja niezachowana");
        return ok ? 0 : 1;
    }

    private static int ChildExpectsMissing(string path)
    {
        var read = new SonosDpapiCredentialStore(Broker(BrokerOrigin), path).Read();
        var ok = read.Status == SonosCredentialReadStatus.Missing && read.Credentials is null;
        Console.WriteLine(ok ? "POTOMNY OK: brak zapisu" : "POTOMNY BLAD: nieoczekiwany status " + read.Status);
        return ok ? 0 : 1;
    }

    /// <summary>
    /// Uruchamia TEN SAM harness jako NOWY proces Windows. Przekazujemy tylko
    /// nazwe scenariusza i sciezke - zadnego tokenu w argumentach ani w env.
    /// </summary>
    private static int RunChild(string scenario, string path)
    {
        var host = Environment.ProcessPath;
        var assembly = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(assembly))
        {
            Fail("nowy-proces", "nie ustalono sciezki hosta procesu");
            return 1;
        }

        var info = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(assembly);
        info.ArgumentList.Add(scenario);
        info.ArgumentList.Add(path);

        using var child = Process.Start(info);
        if (child is null)
        {
            Fail("nowy-proces", "proces potomny nie wystartowal");
            return 1;
        }

        var output = child.StandardOutput.ReadToEnd().Trim();
        var error = child.StandardError.ReadToEnd().Trim();
        if (!child.WaitForExit(60_000))
        {
            try
            {
                child.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            Fail("nowy-proces", "przekroczony limit czasu");
            return 1;
        }

        if (output.Length > 0)
        {
            Console.WriteLine("     [potomny] " + output);
        }

        if (error.Length > 0)
        {
            Console.WriteLine("     [potomny/err] " + error);
        }

        return child.ExitCode;
    }

    // ---- narzedzia ----

    /// <summary>
    /// DPAPI biezacego uzytkownika z ta sama entropia co magazyn - wylacznie po
    /// to, by SFALSZOWAC plik z nieobslugiwanym formatem. Ten kod nalezy do
    /// testu, nie do produktu.
    /// </summary>
    private static byte[] Protect(byte[] plaintext)
    {
        var input = Allocate(plaintext);
        var entropy = Allocate(DomainEntropy);
        var output = default(Blob);
        try
        {
            if (!CryptProtectData(ref input, null, ref entropy, IntPtr.Zero, IntPtr.Zero, 0x1, ref output)
                || output.Pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("CryptProtectData nie zaszyfrowal danych testowych");
            }

            var result = new byte[output.Length];
            Marshal.Copy(output.Pointer, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (input.Pointer != IntPtr.Zero) Marshal.FreeHGlobal(input.Pointer);
            if (entropy.Pointer != IntPtr.Zero) Marshal.FreeHGlobal(entropy.Pointer);
            if (output.Pointer != IntPtr.Zero) LocalFree(output.Pointer);
        }
    }

    private static Blob Allocate(byte[] data)
    {
        var pointer = Marshal.AllocHGlobal(Math.Max(data.Length, 1));
        Marshal.Copy(data, 0, pointer, data.Length);
        return new Blob { Length = (uint)data.Length, Pointer = pointer };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public uint Length;
        public IntPtr Pointer;
    }

    [DllImport("crypt32.dll", EntryPoint = "CryptProtectData", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref Blob input,
        string? description,
        ref Blob entropy,
        IntPtr reserved,
        IntPtr prompt,
        uint flags,
        ref Blob output);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);

    private static int CountFiles(string directory) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory).Length : 0;

    private static bool ByteEquals(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var start = 0; start <= haystack.Length - needle.Length; start++)
        {
            var match = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (haystack[start + offset] != needle[offset])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    private static void Check(string name, bool condition)
    {
        if (condition)
        {
            Console.WriteLine("OK   " + name);
            return;
        }

        Fail(name, "warunek niespelniony");
    }

    private static void Fail(string name, string reason)
    {
        _failures++;
        Console.WriteLine("BLAD " + name + ": " + reason);
    }

    private static void TryCleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
