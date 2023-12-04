using System.Collections.Generic;
using CyberArk.Extensions.Plugins.Models;
using CyberArk.Extensions.Utilties.Logger;

// Change the Template name space
namespace CyberArk.Extensions.KubernetesServiceAccount
{
    public class DeleteAfterReconcile : BaseAction
    {
        #region constructor
        /// <summary>
        /// Logon Ctor. Do not change anything unless you would like to initialize local class members
        /// The Ctor passes the logger module and the plug-in account's parameters to base.
        /// Do not change Ctor's definition not create another.
        /// <param name="accountList"></param>
        /// <param name="logger"></param>
        public DeleteAfterReconcile(List<IAccount> accountList, ILogger logger)
            : base(accountList, logger)
        {
        }
        #endregion

        #region Setter
        /// <summary>
        /// Defines the Action name that the class is implementing - DeleteAfterReconcile
        /// </summary>
        override public CPMAction ActionName
        {
            get { return CPMAction.deletepassafterreconcile; }
        }
        #endregion

        /// <summary>
        /// Plug-in Starting point function.
        /// </summary>
        /// <param name="platformOutput"></param>

        override public int run(ref PlatformOutput platformOutput)
        {
            Logger.MethodStart();

            #region Init

            int RC = 0;

            #endregion 

            return RC;

        }

    }
}
