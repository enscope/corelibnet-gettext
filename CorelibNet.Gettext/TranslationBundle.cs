/*
 * TranslationBundle
 * CorelibNet.Gettext
 *
 * Copyright 2013 Enscope, s.r.o. <dev@enscope.com>
 *     Miroslav Hudak <mhudak@enscope.com>
 *
 * License: Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
 *          http://creativecommons.org/licenses/by-sa/3.0/
 */

using CorelibNet.Gettext.Utilities;
using IronRuby;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CorelibNet.Gettext
{
    public class TranslationBundle
    {
        #region Supporting Classes

        private class PluralFormsSolver
        {
            private const string HEADER_MATCH_PATTERN = "^nplurals=(\\d*?); plural=(.*?);$";

            private readonly ScriptEngine _scriptEngine;
            private readonly ScriptSource _scriptSource;
            private readonly ScriptScope _scriptScope;

            private readonly int _numberOfPluralForms;
            private readonly string _expression;

            private PluralFormsSolver(string header)
            {
                var match = Regex.Match(header, HEADER_MATCH_PATTERN);
                if (match == Match.Empty || match.Groups.Count != 3)
                    throw new Exception("Invalid plurals definition.");
                _numberOfPluralForms = int.Parse(match.Groups[1].Value);
                _expression = match.Groups[2].Value;

                _scriptEngine = Ruby.CreateEngine();
                _scriptSource = _scriptEngine.CreateScriptSourceFromString(_expression, SourceCodeKind.Expression);
                _scriptScope = _scriptEngine.CreateScope();
            }

            internal int GetPluralFormIndex(int number)
            {
                try
                {
                    _scriptScope.SetVariable("n", number);
                    var tmp = _scriptSource.Execute(_scriptScope);
                    var result = (tmp is int) ? tmp : (tmp is bool) ? tmp ? 1 : 0 : 0;
                    return (Math.Min(result, _numberOfPluralForms));
                }
                catch (Exception)
                {
                    if (!Silent) throw;
                    return (0);
                }
            }

            internal static PluralFormsSolver Create(string header)
            {
                try
                {
                    return (new PluralFormsSolver(header));
                }
                catch (Exception)
                {
                    if (!Silent) throw;
                    return (null);
                }
            }
        }

        private class HeaderMsg
        {
            private readonly IDictionary<string, string> _headers;

            internal readonly PluralFormsSolver PluralFormsSolver;

            internal HeaderMsg(string headerMsg)
            {
                var headers = new Dictionary<string, string>();
                foreach (var line in headerMsg.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    ParseSingleLine(headers, line);
                }
                _headers = headers;

                // create expression context
                if (_headers.Keys.Contains("Plural-Forms"))
                    PluralFormsSolver = PluralFormsSolver.Create(_headers["Plural-Forms"]);
            }

            private static void ParseSingleLine(IDictionary<string, string> dict, string line)
            {
                var firstColon = line.IndexOf(':');
                if (firstColon <= -1) return;
                dict.Add(line.Substring(0, firstColon).Trim(), line.Substring(firstColon + 1).Trim());
            }

            [Conditional("DEBUG")]
            internal void Dump(TextWriter writer = null)
            {
                writer = writer ?? Console.Out;
                writer.WriteLine("--- HEADER MSG ---");
                foreach (var s in _headers.Keys) writer.WriteLine("{0}: '{1}'", s, _headers[s]);
            }
        }

        #endregion

        #region Properties

        private const uint GETTEXT_MAGIC = 0x950412de;

        private readonly Lazy<ConcurrentDictionary<string, string[]>> _stringsLazy = 
            new Lazy<ConcurrentDictionary<string, string[]>>(() => new ConcurrentDictionary<string, string[]>());
        private ConcurrentDictionary<string, string[]> Strings 
        {
            get { return (_stringsLazy.Value); }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FileHeaderStruct
        {
            [FieldOffset(0)] 
            internal readonly uint Magic;
            [FieldOffset(4)]
            internal readonly ushort MajorVersion;
            [FieldOffset(6)]
            internal readonly ushort MinorVersion;
            [FieldOffset(8)]
            internal readonly uint NumberOfStrings;
            [FieldOffset(12)]
            internal readonly uint OriginalTableOffset;
            [FieldOffset(16)]
            internal readonly uint TranslationTableOffset;
            [FieldOffset(20)]
            internal readonly uint HashingTableSize;
            [FieldOffset(24)]
            internal readonly uint HashingTableOffset;
        };

        [StructLayout(LayoutKind.Explicit)]
        private struct StringHeaderStruct
        {
            [FieldOffset(0)]
            internal readonly uint Length;
            [FieldOffset(4)]
            internal readonly uint Offset;
        }

        private FileHeaderStruct _bundleFileHeader;
        private HeaderMsg _headerMsg;

        public readonly CultureInfo Culture;
        
        public string ResourceNamespace = "i18n";
        public string ResourceObjectName = "strings";

        public static bool Silent = true;
        public static TranslationBundle DefaultBundle;

        #endregion

        #region Constructors and Constructor Helpers

        public TranslationBundle(CultureInfo cultureInfo = null)
        {
            Culture = cultureInfo ?? CultureInfo.CurrentUICulture;
        }

        public static TranslationBundle Create(CultureInfo cultureInfo = null, bool isDefaultBundle = true)
        {
            var bundle = new TranslationBundle(cultureInfo);
            if (isDefaultBundle || DefaultBundle == null) bundle.AsDefaultBundle();
            return (bundle);
        }

        public static TranslationBundle CreateCurrentCulture(bool isDefaultBundle = true)
        {
            return (Create(CultureInfo.CurrentCulture, isDefaultBundle));
        }

        public static TranslationBundle CreateCurrentUiCulture(bool isDefaultBundle = true)
        {
            return (Create(CultureInfo.CurrentUICulture, isDefaultBundle));
        }

        #endregion

        #region Loader Methods
        
        public TranslationBundle Load(Assembly assembly = null)
        {
            try
            {
                assembly = assembly ?? Assembly.GetEntryAssembly();
                using (var stream = GetResourceStreamForAssembly(assembly)) Load(stream);
            }
            catch
            {
                if (!Silent) throw;
            }
            
            return (this);
        }

        public TranslationBundle Load(Stream stream)
        {
            try
            {
                PreloadStrings(stream);
            }
            catch
            {
                if (!Silent) throw;
            }
            
            return (this);
        }

        public TranslationBundle Load(string resourceName, Assembly assembly = null)
        {
            try
            {
                assembly = assembly ?? Assembly.GetEntryAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName)) Load(stream);
            }
            catch
            {
                if (!Silent) throw;
            }
            
            return (this);
        }

        public TranslationBundle LoadSatellite(Assembly parentAssembly = null, Version assemblyVersion = null)
        {
            try
            {
                parentAssembly = parentAssembly ?? Assembly.GetEntryAssembly();
                var parentAssemblyName = new AssemblyName(parentAssembly.FullName);

                var assembly = parentAssembly.GetSatelliteAssembly(Culture, assemblyVersion);
                var resourceNames = assembly.GetManifestResourceNames();
                if (resourceNames.Length != 1)
                    throw new IOException("Satellite resource must contain exactly one manifest resource.");
                var resourceName = string.Format("{0}.{1}.{2}", parentAssemblyName.Name, ResourceNamespace, Culture.Name);

                var resourceManager = new ResourceManager(resourceName, assembly);
                var obj = resourceManager.GetObject(ResourceObjectName);
                if (!(obj is byte[])) throw new IOException("Invalid satellite resource object type.");
                var stream = new MemoryStream((byte[])obj);

                using (stream) Load(stream);
            }
            catch
            {
                if (!Silent) throw;
            }

            return (this);
        }

        #endregion

        #region Public Methods

        public TranslationBundle AsDefaultBundle()
        {
            DefaultBundle = this;
            return (this);
        }

        public string GetString(string text)
        {
            try
            {
                return (GetStrings(text)[0]);
            }
            catch
            {
                if (!Silent) throw;
                return (text);
            }
        }

        public string GetStringNum(string singular, string plural, int number)
        {
            try
            {
                return (number == 1)
                           ? (string.Format(GetString(singular), number))
                           : (string.Format(GetPluralString(plural, number), number));
            }
            catch
            {
                if (!Silent) throw;
                return string.Format((number != 1) ? plural : singular, number);
            }
        }

        #endregion

        #region Internal Methods

        protected Stream GetResourceStreamForAssembly(Assembly assembly)
        {
            var assemblyName = new AssemblyName(assembly.FullName);
            var ns = string.Format("{0}.{1}", assemblyName.Name, ResourceNamespace);

            Stream stream = null;
            if (Culture.Name != Culture.TwoLetterISOLanguageName)
            {
                var resourceName = string.Format("{0}.{1}.{2}.mo", ns, Culture.TwoLetterISOLanguageName, Culture.Name);
                stream = assembly.GetManifestResourceStream(resourceName);
            }
            if (stream == null)
            {
                var resourceName = string.Format("{0}.{1}.{1}.mo", ns, Culture.TwoLetterISOLanguageName);
                stream = assembly.GetManifestResourceStream(resourceName);
            }
            return (stream);
        }

        protected void PreloadStrings(Stream input)
        {
            try
            {
                if (Strings.Count > 0) Strings.Clear();

                _bundleFileHeader = input.ReadStruct<FileHeaderStruct>();
                if (_bundleFileHeader.Magic != GETTEXT_MAGIC)
                    throw new IOException("Invalid MAGIC");

                var originalHeaders = ReadStringHeaders(input, _bundleFileHeader.OriginalTableOffset,
                                                        _bundleFileHeader.NumberOfStrings);
                var translationHeaders = ReadStringHeaders(input, _bundleFileHeader.TranslationTableOffset,
                                                           _bundleFileHeader.NumberOfStrings);

                var originals = new string[_bundleFileHeader.NumberOfStrings][];
                var translations = new string[_bundleFileHeader.NumberOfStrings][];
                for (var x = 0; x < _bundleFileHeader.NumberOfStrings; x++)
                    originals[x] = ReadTextEntry(input, originalHeaders[x].Offset, originalHeaders[x].Length);
                for (var x = 0; x < _bundleFileHeader.NumberOfStrings; x++)
                    translations[x] = ReadTextEntry(input, translationHeaders[x].Offset, translationHeaders[x].Length);

                for (var x = 0; x < _bundleFileHeader.NumberOfStrings; x++)
                {
                    switch (originals[x].Length)
                    {
                        case 1:
                            Strings.TryAdd(originals[x][0], translations[x]);
                            break;
                        case 2:
                            Strings.TryAdd(originals[x][0], new[] {translations[x][0]});
                            Strings.TryAdd(originals[x][1], translations[x]);
                            break;
                    }
                }
            }
            finally
            {
                _headerMsg = new HeaderMsg(GetString(string.Empty));
            }
        }

        private static StringHeaderStruct[] ReadStringHeaders(Stream input, uint offset, uint count)
        {
            var headers = new StringHeaderStruct[count];
            input.Seek(offset, SeekOrigin.Begin);
            for (var x = 0; x < count; x++)
                headers[x] = input.ReadStruct<StringHeaderStruct>();
            return (headers);
        }

        private static string[] ReadTextEntry(Stream input, uint offset, uint length)
        {
            if (input.Position != offset) input.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[length + 1];
            input.Read(buffer, 0, (int)length + 1);
            IList<string> strings = new List<string>();
            var istart = 0;
            for (var i = 0; i < length + 1; i++)
            {
                if (buffer[i] != '\0') continue;
                strings.Add(Encoding.UTF8.GetString(buffer, istart, i - istart));
                istart = i + 1;
            }
            return (strings.ToArray());
        }

        private string[] GetStrings(string text)
        {
            string[] translations;
            if (Strings.TryGetValue(text, out translations))
            {
                return (translations ?? new[] {text});
            }
            return (new[] {text});
        }

        private string GetPluralString(string plural, int number)
        {
            if (_headerMsg.PluralFormsSolver == null) return (plural);
            var pluralFormIdx = _headerMsg.PluralFormsSolver.GetPluralFormIndex(number);
            var pluralStrings = GetStrings(plural);
            var pluralString = (pluralStrings.Length > pluralFormIdx) ? pluralStrings[pluralFormIdx] : plural;
            return (string.Format(pluralString, number));
        }

        #endregion

        #region Debug Methods

        [Conditional("DEBUG")]
        public void Dump(TextWriter writer = null)
        {
            writer = writer ?? Console.Out;

            DumpHeader(writer);
            _headerMsg.Dump(writer);
            DumpStrings(writer);
        }

        [Conditional("DEBUG")]
        public void DumpHeader(TextWriter writer = null)
        {
            writer = writer ?? Console.Out;
            writer.WriteLine("--- FILE HEADER ---");
            writer.WriteLine("MAGIC:        {0}", _bundleFileHeader.Magic);
            writer.WriteLine("FILE FORMAT:  {0}.{1}", _bundleFileHeader.MajorVersion, _bundleFileHeader.MinorVersion);
            writer.WriteLine("# OF STRINGS: {0}", _bundleFileHeader.NumberOfStrings);
            writer.WriteLine("OFFSET ORIG:  {0}", _bundleFileHeader.OriginalTableOffset);
            writer.WriteLine("OFFSET TRANS: {0}", _bundleFileHeader.TranslationTableOffset);
            writer.WriteLine("OFFSET HASH:  {0}", _bundleFileHeader.HashingTableOffset);
            writer.WriteLine("SIZE HASH:    {0}", _bundleFileHeader.HashingTableSize);
        }

        [Conditional("DEBUG")]
        public void DumpStrings(TextWriter writer = null)
        {
            writer = writer ?? Console.Out;
            writer.WriteLine("--- STRINGS ---");
            foreach (var s in Strings.Keys)
                writer.WriteLine("=> '{0}' = '{1}'", s, string.Join("', '", GetStrings(s)));
        }

        #endregion
    }
}
