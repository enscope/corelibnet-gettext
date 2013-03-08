/*
 * Program
 * CorelibNet.Gettext.Demo
 *
 * Copyright 2013 Enscope, s.r.o. <dev@enscope.com>
 *     Miroslav Hudak <mhudak@enscope.com>
 *
 * License: Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
 *          http://creativecommons.org/licenses/by-sa/3.0/
 */

using CorelibNet.Gettext.Utilities;
using System;
using System.Globalization;

namespace CorelibNet.Gettext.Demo
{
    class Program
    {
        static void TestCulture(CultureInfo cultureInfo)
        {
            Console.WriteLine();
            Console.WriteLine("--- CULTURE: {0}", cultureInfo);

            try
            {
                var bundle = TranslationBundle.Create(cultureInfo).LoadSatellite();

                Console.WriteLine(bundle.GetString("This is a test."));
                Console.WriteLine(bundle.GetString("Testing this translation facility."));
                Console.WriteLine(bundle.GetString("These strings will be automatically translated."));
                Console.WriteLine(bundle.GetString("And this is one more string to translate."));

                Console.WriteLine();

                for (var x = 0; x < 20; x++)
                {
                    Console.WriteLine(bundle.GetStringNum("{0} file", "{0} files", x));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: {0}", ex.Message);
            }
        }

        static void Main()
        {
            foreach (var c in new[] { 
                CultureInfo.CurrentCulture,
                CultureInfo.CurrentUICulture,
                CultureInfo.GetCultureInfo("sk-SK"),
                CultureInfo.GetCultureInfo("de-DE"),
                CultureInfo.GetCultureInfo("pl-PL")}
                ) TestCulture(c);

            TranslationBundle.CreateCurrentCulture().LoadSatellite().AsDefaultBundle();

            Console.WriteLine();
            Console.WriteLine(Translator._n("{0} file", "{0} files", int.MaxValue));
            Console.WriteLine(Translator.Format("This is some translatable text with {0} parameter.", "NON-translatable"));

            Console.WriteLine();
            TranslationBundle.DefaultBundle.Dump();

            Console.ReadLine();
        }
    }
}
