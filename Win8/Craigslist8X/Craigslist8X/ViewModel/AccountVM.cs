using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using WB.Craigslist8X.Model;

namespace WB.Craigslist8X.ViewModel
{
    public sealed class AccountVM : AccountVMBase
    {
        public AccountVM(PasswordCredential account)
        {
            this._account = account;
        }

        public override bool ShowDelete
        {
            get
            {
                return true;
            }
        }

        public override string Display
        {
            get { return this._account.UserName; }
        }

        public override ViewModelType Type
        {
            get { return ViewModelType.Account; }
        }

        public PasswordCredential Account
        {
            get
            {
                return this._account;
            }
        }

        PasswordCredential _account;
    }

    public sealed class AnonymousVM : AccountVMBase
    {
        public override string Display
        {
            get { return "Post without account"; }
        }

        public override ViewModelType Type
        {
            get { return ViewModelType.Anonymous; }
        }
    }

    public sealed class AddAccountVM : AccountVMBase
    {
        public override bool ShowArrow
        {
            get
            {
                return false;
            }
        }

        public override string Display
        {
            get { return "Add new account"; }
        }

        public override ViewModelType Type
        {
            get { return ViewModelType.Add; }
        }
    }

    public abstract class AccountVMBase
    {
        public virtual bool ShowArrow
        {
            get
            {
                return true;
            }
        }

        public virtual bool ShowDelete
        {
            get
            {
                return false;
            }
        }

        public abstract string Display
        {
            get;
        }

        public abstract ViewModelType Type
        {
            get;
        }

        public enum ViewModelType
        {
            Account,
            Anonymous,
            Add,
        }
    }
}
