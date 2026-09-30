using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// F1b: ODCZYT ULUBIONYCH domu przez ISTNIEJACEGO koordynatora konta i jego
/// zabezpieczenia generacji. Tylko Core: zero GUI, zero prawdziwego konta,
/// zero sieci - dane syntetyczne, atrapa bramki logowania i atrapa magazynu.
///
/// Ten plik zaczyna sie od testu RED: publicznego wejscia
/// SonosAccountCoordinator.ReadFavoritesAsync(ISonosFavoritesApi, string?,
/// CancellationToken) JESZCZE NIE MA. Test sprawdza to REFLEKSJA, zeby zestaw
/// sie kompilowal (BUILD=0) i mimo to konczyl niepowodzeniem (TEST=1) z powodu
/// braku funkcji, nie z powodu bledu typow.
/// </summary>
internal static class SonosFavoritesAccountTests
{
    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("koordynator konta wystawia odczyt ulubionych", CoordinatorExposesFavoritesRead)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Odczyt ulubionych Sonos przez konto: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Odczyt ulubionych Sonos przez konto: " + failures + " nieudanych testow.");
        }
    }

    private static void CoordinatorExposesFavoritesRead()
    {
        var method = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "ReadFavoritesAsync"
                && candidate.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[] { typeof(ISonosFavoritesApi), typeof(string), typeof(CancellationToken) }));

        Check(method is not null);
    }

    private static void Check(bool ok)
    {
        if (!ok)
        {
            throw new InvalidOperationException("Niespelniona asercja (dane syntetyczne ukryte).");
        }
    }
}
