﻿//------------------------------------------------------------------------------
// This is auto-generated code.
//------------------------------------------------------------------------------
// This code was generated by Entity Developer tool using EF Core template.
// Code is generated on: 9/30/2020 3:49:34 PM
//
// Changes to this file may cause incorrect behavior and will be lost if
// the code is regenerated.
//------------------------------------------------------------------------------

using System;
using System.Data;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Data.Common;
using System.Collections.Generic;

namespace TwitterService
{
    public partial class TweetKeyPhrase {

        public TweetKeyPhrase()
        {
            OnCreated();
        }

        public virtual long ID
        {
            get;
            set;
        }

        public virtual long TweetID
        {
            get;
            set;
        }

        public virtual string KeyPhrase
        {
            get;
            set;
        }

        #region Extensibility Method Definitions

        partial void OnCreated();

        #endregion
    }

}
