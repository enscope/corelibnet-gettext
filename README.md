corelibnet-gettext
==================

Managed Gettext Library for .NET 4.0+ written in C#.

Usage is pretty simple and straight-forward. Everything, you will ever need to use 
(except library initialization), is in this class (comments and method body omitted):

{
    public class Translator
    {
        public static string Translate(string text) {}
        public static string _(string text) {}
        public static string TranslateNum(string singular, string plural, int number) {}
        public static string _n(string singular, string plural, int number) {}
        public static string Format(string format, object arg0) {}
        public static string Format(string format, params object[] args) {}
    }
}

So the translation is simply done via calling:

myLabel.Text = Translator._("This is some text");
// or with plural forms
myLabelWithPlural.Text = Translator._n("{0} bean", "{0} beans", numBeans);

All kinds of plural forms are accepted, all the official were tested. In current implementation, 
plural expressions are evaluated using managed IronRuby library. It’s a bit overkill and unfortunately 
adds around 1 MB to executable size, but it’s well worth it. I was thinking about creating my own 
expression evaluator later, it’s still a possibility, but I have currently other projects to work on.

For more advanced usage example, please refer to demo application included in the source bundle.
