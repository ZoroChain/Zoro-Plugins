using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace Zoro.Plugins
{
    class TransactionDal
    {
        public MySqlConnection conn = null;
        public MySqlTransaction transaction = null;

        public void BeginTransaction()
        {
            conn = new MySqlConnection(MysqlConn.conf);
            conn.Open();
            transaction = conn.BeginTransaction();
        }

        public void CommitTransaction()
        {
            if (null != transaction)
            {
                transaction.Commit();
            }
        }

        public void RollbackTransaction()
        {
            if (null != transaction)
            {
                transaction.Rollback();
            }

        }

        public void DisposeTransaction()
        {
            if (conn.State == System.Data.ConnectionState.Open)
            {
                conn.Close();
            }
            if (null != transaction)
            {
                transaction.Dispose();
            }

        }
    }
}
