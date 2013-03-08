/*
 * Translator
 * CorelibNet.Gettext
 *
 * Copyright 2013 Enscope, s.r.o. <dev@enscope.com>
 *     Miroslav Hudak <mhudak@enscope.com>
 *
 * License: Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
 *          http://creativecommons.org/licenses/by-sa/3.0/
 */

namespace CorelibNet.Gettext.Utilities
{
    public class Translator
    {
        /// <summary>
        /// Alias for _().
        /// </summary>
        /// <param name="text">Text to be translated</param>
        /// <returns>Translated string</returns>
        public static string Translate(string text)
        {
            return (_(text));
        }

        /// <summary>
        /// Translates string using default translation bundle.
        /// </summary>
        /// <param name="text">Text to be translated</param>
        /// <returns>Translated string</returns>
        public static string _(string text)
        {
            return (TranslationBundle.DefaultBundle != null) 
                ? (TranslationBundle.DefaultBundle.GetString(text)) 
                : (text);
        }

        /// <summary>
        /// Alias for _n()
        /// </summary>
        /// <param name="singular">Singular form of text to be translated</param>
        /// <param name="plural">Plural form of text to be translated</param>
        /// <param name="number">Actual number of items</param>
        /// <returns>Correct plural form for specified language based on number of items</returns>
        public static string TranslateNum(string singular, string plural, int number)
        {
            return (_n(singular, plural, number));
        }

        /// <summary>
        /// Translates text using correct plural form.
        /// </summary>
        /// <param name="singular">Singular form of text to be translated</param>
        /// <param name="plural">Plural form of text to be translated</param>
        /// <param name="number">Actual number of items</param>
        /// <returns>Correct plural form for specified language based on number of items</returns>
        public static string _n(string singular, string plural, int number)
        {
            return string.Format((TranslationBundle.DefaultBundle != null)
                                     ? TranslationBundle.DefaultBundle.GetStringNum(singular, plural, number)
                                     : (number != 1) ? plural : singular, 
                                 number);
        }

        /// <summary>
        /// Translates formatted string.
        /// </summary>
        public static string Format(string format, object arg0)
        {
            return (string.Format(_(format), arg0));
        }

        /// <summary>
        /// Translates formatted string.
        /// </summary>
        public static string Format(string format, params object[] args)
        {
            return (string.Format(_(format), args));
        }
    }
}
