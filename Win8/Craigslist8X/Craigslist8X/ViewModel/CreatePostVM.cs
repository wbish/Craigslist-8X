using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HtmlAgilityPack;
using WB.SDK;

namespace WB.Craigslist8X.ViewModel
{
    public class PickItemVM
    {
        public PickItemVM(string display, HtmlNode node)
        {
            this.Display = Utilities.HtmlToText(display);
            this.Node = node;
            this.Name = node.Attributes["name"].Value;
            this.Value = node.Attributes["value"].Value;
        }

        public string Display
        {
            get;
            set;
        }

        public HtmlNode Node
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            set;
        }

        public string Value
        {
            get;
            set;
        }
    }

    public class FormFieldItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Text { get; set; }

        public FormFieldItem(string name, string value, string text)
        {
            this.Name = name;
            this.Value = value;
            this.Text = text;
        }
    }

    public class FormHiddenField
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public FormHiddenField(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }
    }

    public class EventDateFields
    {
        public string Year
        {
            get;
            set;
        }

        public string Month
        {
            get;
            set;
        }

        public string Day
        {
            get;
            set;
        }
    }

    public class ImageBoxVM
    {
        public ImageBoxVM()
        {
            this.HiddenFields = new List<FormHiddenField>();
        }

        public Uri Image
        {
            get;
            set;
        }

        public List<FormHiddenField> HiddenFields
        {
            get;
            set;
        }

        public virtual bool ShowDelete
        {
            get 
            { 
                return true;
            }
        }
    }

    public class AddImageBoxVM : ImageBoxVM
    {
        public override bool ShowDelete
        {
            get
            {
                return false;
            }
        }
    }
}
