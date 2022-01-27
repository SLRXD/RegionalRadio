using System;
using System.Data;
using System.Windows.Forms;

namespace RegionalRadio
{
    public partial class Edit : Form
    {
        public Edit(string RegionCode)
        {
            InitializeComponent();
            string str = string.Format(@"select * from BroadCast where RegionCode='" + RegionCode + "'");
            DataTable dt = DbHelperSQLUp.ExecuteDataTable(str);
            lbRegionCode.Text = dt.Rows[0]["RegionCode"].ToString();
            lbRegionName.Text = dt.Rows[0]["RegionName"].ToString();
            tbDuration.Text = dt.Rows[0]["Duration"].ToString();
            tbCycleTime.Text = dt.Rows[0]["CycleTime"].ToString();
            tbIP.Text = dt.Rows[0]["IP"].ToString();
        }

        private void Edit_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string RegionCode = lbRegionCode.Text.Trim();
            string Duration = tbDuration.Text.Trim();
            string CycleTime = tbCycleTime.Text.Trim();
            string ip = tbIP.Text.Trim();
            string str = string.Format(@"update BroadCast set Duration='" + Duration + "',CycleTime='" + CycleTime + "',IP='" + ip + "' where RegionCode='" + RegionCode + "'");
            DbHelperSQLUp.ExecuteSql(str);
            LogHelper.WriteInfoLog(lbRegionName.Text + "设置成功\r\n");
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
