using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.Xml.Linq;
using System.IO;
using System.Threading;
using System.Configuration;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Dts.Runtime;
using DTS = Microsoft.SqlServer.Dts.Runtime;
using Microsoft.SqlServer.Dts.Pipeline;
using Microsoft.SqlServer.Dts.Pipeline.Wrapper;
using Microsoft.SqlServer.Dts.Tasks.ExecutePackageTask;
using Microsoft.SqlServer.Dts.Tasks.ExecuteSQLTask;    // add reference to Microsoft.SqlServer.SqlTask.dll


namespace SSIS_DataFlow
{
    public partial class Form1 : Form
    {
        DTS.Application App = null;
        Package package = null;
        string _userid = null, _password = null, _server = null;
        string _StagingDB = null, _ProdDB = null, _Tablename, _Fieldname, _Tableprefix = null, _Excludetables = null, _Controltable, _SSISConfiguration, _ParentPackage, _DestinationPackage,
        _MonthPackage, _ISeriesPackage, _SQLStatemnet1, _CMProduction, _CMStaging, _CMISeries;
        int _CodePage = 1250;
        bool _ValidateExternalMetadata = true;
        
        public Form1()
        {
            InitializeComponent();
            
            _Tableprefix = ReadSSISConfig("Tableprefix");
            _Excludetables = ReadSSISConfig("Excludetables");            
            _SSISConfiguration = ReadSSISConfig("SSISConfiguration");  
            _ParentPackage = ReadSSISConfig("ParentPackageSignature"); 
            _DestinationPackage = ReadSSISConfig("DestinationPackageSignature");            
            _MonthPackage = ReadSSISConfig("MonthPackageSignature");           
            _ISeriesPackage = ReadSSISConfig("ISeriesPackageSignature");  
            _SQLStatemnet1 = ReadSSISConfig("SQLStatemnet1");      
            _Controltable = ReadSSISConfig("Controltable");
            _StagingDB = ReadSSISConfig("StagingDatabase");
            _ProdDB = ReadSSISConfig("ProductionDatabase");
            _CMProduction = ReadSSISConfig("CMProduction"); 
            _CMStaging = ReadSSISConfig("CMStaging");
            _CMISeries = ReadSSISConfig("CMISeries");
                    
            textBoxStagingDB.Text = _StagingDB ;
            textBoxProdDB.Text = _ProdDB;  

        }
        

        /*Get Packages*/ 
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {                
                App = new DTS.Application();
                _server = ServertextBox.Text;

                if (comboBox1.SelectedItem.ToString() == "SQL Server Authentication")
                {
                    _userid = UsertextBox.Text.Trim();
                    _password = PasswordtextBox.Text.Trim();
                }

                PackageInfos pInfos = App.GetPackageInfos("\\", _server, _userid, _password);

                foreach (PackageInfo pInfo in pInfos)
                {
                    checkedListBox1.Items.Add(pInfo.Name);
                }

                button2.Enabled = true;
                button3.Enabled = true;
            }
            catch(Exception err)
            {
                MessageBox.Show(err.Message + "\r" , "", MessageBoxButtons.OK,MessageBoxIcon.Error );
            }

        }
        
        /*Add new Field */
        private void button2_Click(object sender, EventArgs e)
        {
            pictureBox1.Visible = true;
            pictureBox1.Image = SSIS_DataFlow.Properties.Resources.gears_animated;
            this.toolStripStatusLabel2.Text = "Processing";
            backgroundWorker1.RunWorkerAsync('F');            
        }

        private void Generate_New_Field()
        {
            try
            {
                BackupSSISPackages();

                List<string> list = new List<string>();

                _Tablename = textBoxTableName.Text.Trim();
                _Fieldname = textBoxFieldname.Text.Trim();

                for (int x = 0; x <= checkedListBox1.CheckedItems.Count - 1; x++)
                {

                    if (checkedListBox1.CheckedItems[x].ToString().Contains(_ISeriesPackage))
                    {
                        GetDataFlowTask(checkedListBox1.CheckedItems[x].ToString());
                    }

                    if (checkedListBox1.CheckedItems[x].ToString().Contains(_ParentPackage))
                    {
                        package = App.LoadFromSqlServer(checkedListBox1.CheckedItems[x].ToString(), _server, _userid, _password, null);
                        Executable exec = package.Executables[_SQLStatemnet1];
                        TaskHost tkSQLTask = exec as TaskHost;
                        ExecuteSQLTask tkSQLTask01 = tkSQLTask.InnerObject as ExecuteSQLTask;

                        string newquery = null, src = null;
                        bool foundsql = false;
                        string strRegex1 = @"(INSERT)\s*?\r*?\n*(INTO)\s*\r*\n*(" + _Tableprefix + _Tablename.Remove(0, 2).ToString() + @")\s*?\r*?\n*\([A-Z,0-9,\,\s]*\)\s*select\s+(?:distinct\s+)?(?:top\s+\d*\s+)?(?'columns'.*?)(from)\s+(" + _Tablename + ")";
                        Regex myRegex1 = new Regex(strRegex1, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);

                        string strTargetString1 = tkSQLTask01.SqlStatementSource;


                        foreach (Match myMatch1 in myRegex1.Matches(strTargetString1))
                        {
                            if (myMatch1.Success)
                            {
                                newquery = myMatch1.ToString().Insert(myMatch1.ToString().IndexOf(")"), "," + _Fieldname);

                                newquery = Regex.Replace(newquery, "FROM", "," + _Tablename + "." + _Fieldname + " \r from ", RegexOptions.IgnoreCase);

                                src = myMatch1.ToString();

                                foundsql = true;
                                break;
                            }
                        }

                        if (foundsql)
                        {
                            strTargetString1.Replace(src, newquery);
                            var sqlstr = strTargetString1.Replace(src, newquery);
                            tkSQLTask01.SqlStatementSource = sqlstr;
                        }
                        else
                        {
                            MessageBox.Show("Cannot find reference to INSERT or SELECT for Table " + _Tablename + ".\nPlease Manually Check The SQL Statements In The Package.",
                                            "  ",
                                            MessageBoxButtons.OK,
                                            MessageBoxIcon.Exclamation);
                        }


                        Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();
                        app.SaveToSqlServer(package, null, _server, _userid, _password);

                    }
                }               
 
            }
            catch (Exception ea)
            {
                MessageBox.Show(ea.Data + " " + ea.Message + " " + ea.Source + " " + ea.StackTrace,"Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
                throw new System.Exception("Error occurred on worker thread", ea);
            } 
        }

        private void Generate_New_Table()
        {
            try
            {
                pictureBox1.Visible = true;
                pictureBox1.Image = SSIS_DataFlow.Properties.Resources.gears_animated;

                BackupSSISPackages();

                List<string> list = new List<string>();

                _StagingDB = textBoxStagingDB.Text;
                _ProdDB = textBoxProdDB.Text;
                _Tablename = textBoxTableName.Text;
                _Fieldname = textBoxFieldname.Text;

                string SQLDelete = null, SQLInsert = null;


                for (int x = 0; x <= checkedListBox1.CheckedItems.Count - 1; x++)
                {
                    list.Add(checkedListBox1.CheckedItems[x].ToString());

                    if (checkedListBox1.CheckedItems[x].ToString().Contains(_ISeriesPackage))
                    {
                        listBox1.Items.Add("Creating new Table in SQL Server");
                        AddNewTable(checkedListBox1.CheckedItems[x].ToString());


                    }
                    #region Create SQL
                    if (checkedListBox1.CheckedItems[x].ToString().Contains(_DestinationPackage))
                    {

                        package = App.LoadFromSqlServer(checkedListBox1.CheckedItems[x].ToString(), _server, _userid, _password, null);
                        ConnectionManager CmDest = package.Connections[_CMProduction];
                        ConnectionManager CmSource = package.Connections[_CMStaging];

                        OleDbConnectionStringBuilder builderDest = new OleDbConnectionStringBuilder();
                        builderDest.ConnectionString = CmDest.ConnectionString;
                        var InitialCatalogProd = builderDest["Initial Catalog"];

                        OleDbConnectionStringBuilder builderSource = new OleDbConnectionStringBuilder();
                        builderSource.ConnectionString = CmSource.ConnectionString;
                        var InitialCatalogStaging = builderSource["Initial Catalog"];


                        var sqlstatementsource = "if exists (select * from dbo.sysobjects where id = object_id(N'[dbo].[" + _Tableprefix + _Tablename.Substring(2) + "]') and OBJECTPROPERTY(id, N'IsUserTable') = 1) " +
                            " TRUNCATE TABLE [" + InitialCatalogProd + "].." + _Tableprefix + _Tablename.Substring(2);


                        TaskHost Task1 = CreateExecuteSQLTask(package,
                                               "Truncate Table " + _Tableprefix + _Tablename.Substring(2),
                                               CmDest.ID,
                                               sqlstatementsource,
                                               _CodePage);

                        sqlstatementsource = " INSERT INTO [" + InitialCatalogProd + "].." + _Tableprefix + _Tablename.Substring(2) + "  SELECT * FROM " + InitialCatalogStaging + ".." + _Tableprefix + _Tablename.Substring(2);


                        TaskHost Task2 = CreateExecuteSQLTask(package,
                                               "Insert " + _Tableprefix + _Tablename.Substring(2),
                                               CmDest.ID,
                                               sqlstatementsource,
                                               _CodePage);


                        PrecedenceConstraint preCons = package.PrecedenceConstraints.Add(Task1, Task2);
                        preCons.Name = "Precedant " + _Tableprefix + _Tablename.Substring(2);
                        preCons.LogicalAnd = true;
                        preCons.EvalOp = DTSPrecedenceEvalOp.Constraint;
                        preCons.Value = DTSExecResult.Success;


                        Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();
                        app.SaveToSqlServer(package, null, _server, _userid, _password);

                    }
                    #endregion

                    #region Append SQL
                    if (checkedListBox1.CheckedItems[x].ToString().Contains(_MonthPackage))
                    {
                        package = App.LoadFromSqlServer(checkedListBox1.CheckedItems[x].ToString(), _server, _userid, _password, null);

                        //Get table schema from production database
                        ConnectionManager CmDest = package.Connections[_CMProduction];
                        ConnectionManager CmSource = package.Connections[_CMStaging];

                        OleDbConnectionStringBuilder ConStrDest = new OleDbConnectionStringBuilder();
                        ConStrDest.ConnectionString = CmDest.ConnectionString;
                        ConStrDest.Remove("provider");

                        OleDbConnectionStringBuilder ConStrSrc = new OleDbConnectionStringBuilder();
                        ConStrSrc.ConnectionString = CmSource.ConnectionString;
                        ConStrSrc.Remove("provider");


                        Popup NewPopup = new Popup();
                        GetSQLTableSchema(NewPopup, _Tablename, CmDest.ConnectionString, _password);

                        DialogResult dialogresult = NewPopup.ShowDialog();

                        if (dialogresult == DialogResult.OK)
                        {

                            SQLDelete = " DELETE FROM " + _Tableprefix + _Tablename.Substring(2) + " WHERE " + _Tableprefix + _Tablename.Substring(2) + "." + NewPopup.GetSelectedCheckListBoxItem() + _Controltable;

                            SQLInsert = " INSERT INTO " + ConStrDest["Initial Catalog"] + ".dbo." + _Tableprefix + _Tablename.Substring(2) + "\n" + " SELECT * FROM " + ConStrSrc["Initial Catalog"] + ".dbo." + _Tableprefix + _Tablename.Substring(2);
                            MessageBox.Show("You clicked OK:" + SQLDelete + "   " + SQLInsert);
                        }
                        else if (dialogresult == DialogResult.Cancel)
                        {
                            MessageBox.Show("You clicked either Cancel or X button in the top right corner");
                            NewPopup.Dispose();
                            break;
                        }

                        NewPopup.Dispose();

                        TaskHost Task3 = CreateExecuteSQLTask(package,
                                                                "Delete Table " + _Tableprefix + _Tablename.Substring(2),
                                                                CmDest.ID,
                                                                SQLDelete,
                                                                _CodePage);

                        TaskHost Task4 = CreateExecuteSQLTask(package,
                                                               "Append Table " + _Tableprefix + _Tablename.Substring(2),
                                                               CmDest.ID,
                                                               SQLInsert,
                                                               _CodePage);

                        PrecedenceConstraint preCons = package.PrecedenceConstraints.Add(Task3, Task4);
                        preCons.Name = "Precedant " + _Tableprefix + _Tablename.Substring(2);
                        preCons.LogicalAnd = true;
                        preCons.EvalOp = DTSPrecedenceEvalOp.Constraint;
                        preCons.Value = DTSExecResult.Success;

                        Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();
                        app.SaveToSqlServer(package, null, _server, _userid, _password);
                    }
                    #endregion

                    #region Update SQL
                    if (checkedListBox1.CheckedItems[x].ToString().Contains(_ParentPackage))
                    {

                        package = App.LoadFromSqlServer(checkedListBox1.CheckedItems[x].ToString(), _server, _userid, _password, null);

                        if (_Tablename != _Excludetables)
                        {
                            Executable exec = package.Executables[_SQLStatemnet1];
                            TaskHost tkSQLTask = exec as TaskHost;
                            ExecuteSQLTask tkSQLTask01 = tkSQLTask.InnerObject as ExecuteSQLTask;

                            var sqlstr = "\n" + " if exists (select * from dbo.sysobjects where id = object_id(N'[dbo].[" + _Tableprefix + _Tablename.Substring(2) + "]') and OBJECTPROPERTY(id, N'IsUserTable') = 1)" + "\n" +
                             " TRUNCATE table [dbo].[" + _Tableprefix + _Tablename.Substring(2) + "]" + "\n" +
                             " GO " + "\n" + " INSERT INTO " + _Tableprefix + _Tablename.Substring(2) + "\n" +
                             " SELECT * FROM " + _Tablename + "\n" + " GO ";
                            tkSQLTask01.SqlStatementSource += sqlstr;
                        }
                        else if (_Tablename == _Excludetables)
                        {
                            MessageBox.Show("New fields for Table " + _Excludetables + " have to be added manually");
                        }

                        Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();
                        app.SaveToSqlServer(package, null, _server, _userid, _password);
                    }
                    #endregion


                }
                pictureBox1.Image = SSIS_DataFlow.Properties.Resources.check_mark;
                toolStripStatusLabel2.Text = "Success";
            }
            catch (Exception ea)
            {
                pictureBox1.Image = SSIS_DataFlow.Properties.Resources.CloseError;
                MessageBox.Show(ea.Data + " " + ea.Message + " " + ea.Source + " " + ea.StackTrace);
                toolStripStatusLabel2.Text = "Error";

            }
        }



        private void button3_Click(object sender, EventArgs e)
        {
            pictureBox1.Visible = true;
            pictureBox1.Image = SSIS_DataFlow.Properties.Resources.gears_animated;
            this.toolStripStatusLabel2.Text = "Processing";
            backgroundWorker1.RunWorkerAsync('T');
        }

        private void GetDataFlowTask(string PackageName)
        {
            // this addes new field in Existing DataFlow Task Table
            try
            {
                var sourceAdapter = _CMISeries+_Tablename.Trim(); 
                var destinationAdapter = _CMStaging+_Tablename.Trim();

                package = App.LoadFromSqlServer(PackageName, _server, _userid, _password, null);

                Executable exec = package.Executables[_Tablename.Trim()];   
                TaskHost DataFlowTask = exec as TaskHost;                
                MainPipe dataFlow = DataFlowTask.InnerObject as MainPipe;
              
                
                //------------------------------------------------------------------------------------
                #region Source Output

                IDTSComponentMetaData100 DataFlowSource = dataFlow.ComponentMetaDataCollection[sourceAdapter]; //                 
                DataFlowSource.ComponentClassID = "DTSAdapter.OleDbSource.2";                
            
                CManagedComponentWrapper SourceInstance = DataFlowSource.Instantiate();// Get the design time instance of the component
               
                SourceInstance.ProvideComponentProperties(); // Initialize the component

                ComponentEventHandler events = new ComponentEventHandler();
                dataFlow.Events = Microsoft.SqlServer.Dts.Runtime.DtsConvert.GetExtendedInterface(events as Microsoft.SqlServer.Dts.Runtime.IDTSComponentEvents);

                ConnectionManager CmSrc = package.Connections[_CMISeries];  
                ConnectionManager CmDest = package.Connections[_CMStaging];   

                OleDbConnectionStringBuilder Db2builder = new OleDbConnectionStringBuilder(); // build string to create new Table with
                Db2builder.ConnectionString = (GetSSISConnectionString(CmSrc.Name));

                OleDbConnectionStringBuilder Stagingbuilder = new OleDbConnectionStringBuilder(); // build string to create new Table with
                Stagingbuilder.ConnectionString = (GetSSISConnectionString(CmDest.Name));
 

                CmSrc.ConnectionString += "Password=" + Db2builder["Password"].ToString() + ";Network Transport Library=TCP;";
                CmDest.ConnectionString += "Password=" + Stagingbuilder["Password"].ToString();


                SourceInstance.SetComponentProperty("DefaultCodePage", _CodePage);
                SourceInstance.SetComponentProperty("AlwaysUseDefaultCodePage", true);
                                         
                SourceInstance.SetComponentProperty("AccessMode", 2);  // 0=TableName,1=variable tablename,2=sql command     
                SourceInstance.SetComponentProperty("SqlCommand", "SELECT * FROM "+_Tablename.Trim()); //0=OpenRowset,(dbo.TableName);1=OpenRowsetVariable,(User::test);2=SqlCommand,(Select * from Product);

                DataFlowSource.RuntimeConnectionCollection[0].ConnectionManager = DtsConvert.GetExtendedInterface(CmSrc);
                DataFlowSource.RuntimeConnectionCollection[0].ConnectionManagerID = CmSrc.ID;
               
                SourceInstance.AcquireConnections(null);
                SourceInstance.ReinitializeMetaData();
                SourceInstance.ReleaseConnections();
                                 
                
                #endregion

                //------------------------------------------------------------------------------------
                             
                #region Destination Input
                IDTSComponentMetaData100 DataFlowDest = dataFlow.ComponentMetaDataCollection[destinationAdapter];                
                DataFlowDest.ComponentClassID = "DTSAdapter.OleDbDestination.2";
                
                CManagedComponentWrapper DestInstance = DataFlowDest.Instantiate();
                
                DestInstance.ProvideComponentProperties();

                DataFlowDest.RuntimeConnectionCollection[0].ConnectionManager = DtsConvert.GetExtendedInterface(CmDest);
                DataFlowDest.RuntimeConnectionCollection[0].ConnectionManagerID = CmDest.ID;

                DestInstance.SetComponentProperty("DefaultCodePage", _CodePage);
                DestInstance.SetComponentProperty("AlwaysUseDefaultCodePage", true);
              
                DestInstance.SetComponentProperty("AccessMode", 3);
                DestInstance.SetComponentProperty("OpenRowset", "[" + _StagingDB + "].[dbo].[" + _Tablename + "]");
              
                DestInstance.AcquireConnections(null);
                DestInstance.ReinitializeMetaData();
                DestInstance.ReleaseConnections();
               
                #endregion
                
                

                //------------------------------------------------------------------------------------ 

                // Create the path from DataFlow Source to destination.
                #region inputMetaData

                IDTSPath100 pathOleSource_Dest = dataFlow.PathCollection[0];
                pathOleSource_Dest.AttachPathAndPropagateNotifications(DataFlowSource.OutputCollection[0], DataFlowDest.InputCollection[0]);

                IDTSInput100 input = DataFlowDest.InputCollection[0] ;
                IDTSVirtualInput100 vInput = input.GetVirtualInput();
                IDTSVirtualInputColumn100 vColumn = vInput.VirtualInputColumnCollection.GetVirtualInputColumnByName("", _Fieldname ); // Get Metaschema detail of Iseries Field
                IDTSInputColumn100 inputColumn = null;
                
                try
                {       
                   if (!FieldExists(_Tablename, _Fieldname, CmDest.ConnectionString, _password)   )
                      {
                            listBox1.Items.Add(string.Format("ID:{0}: \r SourceLocale:{1}: \r LineageID:{2}: \r Name:{3}: \n DataType:{4}: \r Precision:{5}: \r Scale:{6}: \r Length:{7}: \r vColumnID{8}",
                                            input.ID.ToString(),
                                            input.SourceLocale,
                                            vColumn.LineageID,
                                            vColumn.Name,
                                            vColumn.DataType,
                                            vColumn.Precision,
                                            vColumn.Scale,
                                            vColumn.Length,
                                            vColumn.ID
                                           ));

                            AddNewField(_Tablename, vColumn, CmDest.ConnectionString, _password);   //Create new field in Sql Server,
                             
                       }

                    foreach(IDTSVirtualInputColumn100 Col in vInput.VirtualInputColumnCollection )
                    {
                        if (FieldExists(_Tablename, Col.Name, CmDest.ConnectionString, _password))
                        {
                            inputColumn = DestInstance.SetUsageType(input.ID, vInput, Col.LineageID, DTSUsageType.UT_READWRITE);


                            if (vColumn.Name == Col.Name) { CreateInputExternalMetaDataColumn(input.ExternalMetadataColumnCollection, inputColumn); }  // Only create per new field
                            

                            IDTSExternalMetadataColumn100 externalColumn = input.ExternalMetadataColumnCollection[inputColumn.Name]; // Find external column by name 
                            
                            DestInstance.MapInputColumn(input.ID, inputColumn.ID, externalColumn.ID); // Map input column to external column

                            inputColumn.ExternalMetadataColumnID = externalColumn.ID;

                            externalColumn.Name = inputColumn.Name;

                            listBox1.Items.Add("Configuring Columns to Package  " + Col.Name);
                        }
                        else
                        { 
                            inputColumn = DestInstance.SetUsageType(input.ID, vInput, Col.LineageID, DTSUsageType.UT_IGNORED); // Select column, and retain new input column 
                            listBox1.Items.Add("Ignoring Columns  " + Col.Name);
                        }
                    }
                        
                    
                    DataFlowSource.Name = _CMISeries+_Tablename; 
                    DataFlowDest.Name = _CMStaging+_Tablename;

                    Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();
                    app.SaveToSqlServer(package, null, _server, _userid, _password);
                    

                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Data + " " + e.Message + " " + e.Source + " " + e.StackTrace);
                    throw new System.Exception("Error occurred on worker thread", e);
                }
                #endregion
            }
            
            catch (Exception e)
            {
                MessageBox.Show(e.Data + " " + e.Message + " " + e.Source + " " + e.StackTrace);
                throw new System.Exception("Error occurred on worker thread", e);
            }
        }
        
        public void CreateInputExternalMetaDataColumn(IDTSExternalMetadataColumnCollection100 externalCollection, IDTSInputColumn100 column)
        {              
            
            IDTSExternalMetadataColumn100 eColumn = externalCollection.New();

            eColumn.Name = column.Name;
             
            if (column.DataType == Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType.DT_DECIMAL)
            { eColumn.DataType = Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType.DT_R8; }
            else { eColumn.DataType = column.DataType; }

            eColumn.Precision = column.Precision;
            eColumn.Scale = column.Scale;
            eColumn.Length = column.Length;
            eColumn.CodePage = column.CodePage;

            column.ExternalMetadataColumnID = eColumn.ID;             
        }

                      
        private bool FieldExists(string tablename, string fieldname, string ConStr, string SQLPwd)
        {
            bool status = false;
             
 
            OleDbConnectionStringBuilder ConStrPrd = new OleDbConnectionStringBuilder(); 
            ConStrPrd.ConnectionString = (ConstructConnectionString());


            int count = 0;
            string cmdtext = " SELECT count(columns.name) FROM " + _StagingDB + ".sys.tables " +
                             " INNER JOIN " + _StagingDB + ".sys.columns ON tables.object_id= columns.object_id " +
                             " WHERE type='U' " +
                             " AND  tables.name='"+tablename+"' AND columns.name='"+fieldname +"'" ;


            using (var conn = new SqlConnection(ConStrPrd.ConnectionString))
            {
                using (var command = new SqlCommand(cmdtext, conn) { CommandType = CommandType.Text })
                {
                    conn.Open();
                    count =  (Int32) command.ExecuteScalar();
                    conn.Close();
                    
                }
            }
            status = (count > 0 ?  true : false);
            
            return  status;
        }
        
        private string ContructSQLObject(string tablename, IDTSVirtualInputColumn100 vColumn)  // Create field on SQL Table
        {
            
            string InputDataType = vColumn.DataType.ToString();
            string AlterTable = null;
            
            switch(InputDataType)
            {
                case "DT_STR" :
                    AlterTable = "ALTER TABLE " + tablename + " ADD " + vColumn.Name + " VARCHAR(" +  vColumn.Length + ")";
                    break;

                case "DT_DECIMAL" :
                    AlterTable = "ALTER TABLE " + tablename + " ADD " + vColumn.Name + " FLOAT "; //vColumn.Scale                        
                    break;

                case "DT_R8" :
                    AlterTable = "ALTER TABLE " + tablename + " ADD " + vColumn.Name + " FLOAT "; //vColumn.Scale
                    break;

                case "DT_WSTR" :
                    AlterTable = "ALTER TABLE " + tablename + " ADD " + vColumn.Name + " NVARCHAR(" + vColumn.Length + ")";                          
                    break;

                case "DT_DBDATE" :
                    AlterTable = "ALTER TABLE " + tablename + " ADD " + vColumn.Name + " DateTime  ";
                    break;                
            }

            return AlterTable;
        }
        
        private bool AddNewField(string tablename, IDTSVirtualInputColumn100 vColumn, string ConStr, string SQLPwd)
        {
            try
            {

                var dbStagingCC     = " USE [" + _StagingDB + "] " + ContructSQLObject(_Tablename, vColumn);
                var dbStagingPrePod = " USE [" + _StagingDB + "] " + ContructSQLObject(_Tableprefix + _Tablename.Remove(0, 2).ToString(), vColumn);
                var dbProd          = " USE [" + _ProdDB + "] "    + ContructSQLObject(_Tableprefix + _Tablename.Remove(0, 2).ToString(), vColumn);
                int count = 0;
                bool status = false;
 
                OleDbConnectionStringBuilder ConStrPrd = new OleDbConnectionStringBuilder(); 
                ConStrPrd.ConnectionString = (ConstructConnectionString());


                string cmdtext = dbStagingCC + dbStagingPrePod + dbProd;
                 
                using (var conn = new SqlConnection(ConStrPrd.ConnectionString))
                {
                    using (var command = new SqlCommand(cmdtext, conn) { CommandType = CommandType.Text })
                    {
                        conn.Open();
                        count = (Int32)command.ExecuteScalar();
                        conn.Close();
                         
                    }
                }
                status = (count > 0 ? true : false);
                return status;
            }
            catch (Exception e)
            {
                checkedListBox1.Text = e.Message + " " + e.StackTrace;
                return false;
            }
           
            
        }
                
        private void SetAuthenication(object sender, EventArgs e)
        {

            switch (comboBox1.SelectedItem.ToString())
            {
                case "Windows Authentication":
                    UsertextBox.Text = System.Environment.UserDomainName + '\\' + System.Environment.UserName;
                    UsertextBox.ReadOnly = true;
                    labelUserID.Text = "User name:";
                    PasswordtextBox.Enabled = false;
                    PasswordtextBox.Text = "";
                    break;

                case "SQL Server Authentication":
                    labelUserID.Text = "Login:";
                    UsertextBox.ReadOnly = false;
                    UsertextBox.Text = "";
                    PasswordtextBox.Enabled = true;
                    break;
            }

        }
        
        private string ConstructConnectionString()
        {

            var conStr = @";Data Source="+_server+";initial catalog=Master;persistsecurityinfo=true;";

            var authType = comboBox1.SelectedItem.ToString()=="SQL Server Authentication" ? "user id="+_userid+";password="+_password+";" : ";Integrated Security = SSPI;";
            
            return conStr + authType;

        }

        private DataTable GetDB2SchemaData2(string sqlString, string connectionString, string password)
        {
             
                OleDbConnection cn = new OleDbConnection();
                OleDbCommand cmd = new OleDbCommand();
                DataTable schemaTable;
                OleDbDataReader myReader;

                
                cn.ConnectionString = connectionString + "Password=" + password;
                cn.Open();
                 
                //Retrieve records from the   table into a DataReader.
                cmd.Connection = cn;
                cmd.CommandText = sqlString;
                myReader = cmd.ExecuteReader();

                //Retrieve column schema into a DataTable.
                schemaTable = myReader.GetSchemaTable();

                //For each field in the table...
                foreach (DataRow myField in schemaTable.Rows)
                {
                    //For each property of the field...
                    foreach (DataColumn myProperty in schemaTable.Columns)
                    {
                        listBox1.Items.Add(myProperty.ColumnName + " = " + myField[myProperty].ToString());
                    }
                    listBox1.Items.Add("\n\n\n");
                }

                //Always close the DataReader and connection.
                myReader.Close();
                cn.Close();

                return schemaTable;            
        }

        private void AddNewTable(string PackageName)
        {
            try
            {
                package = App.LoadFromSqlServer(PackageName, _server, _userid, _password, null);

                string BuildTable = null, BuildColumnType = null, BuildColumnSize = null, BuildColumnName = null;
                
                ConnectionManager CmSrc = package.Connections[_CMISeries];
                ConnectionManager CmDest = package.Connections[_CMStaging];   

                OleDbConnectionStringBuilder ConStrPrd = new OleDbConnectionStringBuilder(); // build string to create new Table with
                ConStrPrd.ConnectionString = (ConstructConnectionString());
      
                OleDbConnectionStringBuilder Db2builder = new OleDbConnectionStringBuilder(); // build string to create new Table with
                Db2builder.ConnectionString = (GetSSISConnectionString(CmSrc.Name));

                DataTable SchemaTable = GetDB2SchemaData2("SELECT * FROM " +   Db2builder["Default Schema"].ToString() + '.' + _Tablename, CmSrc.ConnectionString, Db2builder["Password"].ToString());
               
                /*
                ColumnName
                DataType System.String, System.Decimal, System.DateTime
                ColumnSize
                SchemaTable.
                */
                                             
                //For each field in the table...
                foreach (DataRow myField in SchemaTable.Rows)
                {
                    //For each property of the field...
                    foreach (DataColumn myProperty in SchemaTable.Columns)
                    {
                        if (myProperty.ColumnName == "ColumnName") { BuildColumnName = myField[myProperty].ToString(); }

                        if (myProperty.ColumnName == "ColumnSize") { BuildColumnSize = myField[myProperty].ToString(); }

                        if (myProperty.ColumnName == "DataType")
                        {
                            switch (myField[myProperty].ToString())                            
                            {   
                                case "System.String":   BuildColumnType = "VARCHAR";  break; 
                                case "System.Decimal":  BuildColumnType = "FLOAT";    break; 
                                case "System.DateTime": BuildColumnType = "DATETIME"; break; 
                            }
                        }
                    }
                    BuildTable += BuildColumnName + " " + BuildColumnType + " " + (BuildColumnType == "VARCHAR" ? "(" + BuildColumnSize + ") " : "") + ",";
                }

                listBox1.Items.Add(BuildTable);
                
                var dbStagingCC = " USE [" + _StagingDB + "] " + " CREATE TABLE " + _Tablename + " ( " + BuildTable.TrimEnd(',') + " ) ";
                var dbStagingPrePod = " USE [" + _StagingDB + "] " + " CREATE TABLE " + _Tableprefix + _Tablename.Remove(0, 2) + " ( " + BuildTable.TrimEnd(',') + " ) ";
                var dbProd = " USE [" + _ProdDB + "] " + " CREATE TABLE " + _Tableprefix + _Tablename.Remove(0, 2) + " ( " + BuildTable.TrimEnd(',') + " ) ";
              
                //Create SQL Server Table                
                string cmdtext = dbStagingCC  + dbStagingPrePod + dbProd;  
                using (var conn = new SqlConnection(ConStrPrd.ConnectionString))
                {
                    using (var command = new SqlCommand(cmdtext, conn) { CommandType = CommandType.Text })
                    {
                        conn.Open();
                        command.ExecuteScalar();
                        conn.Close();
                    }
                }

               
                OleDbConnectionStringBuilder Stagingbuilder = new OleDbConnectionStringBuilder(); // build string to create new Table with
                Stagingbuilder.ConnectionString = (GetSSISConnectionString(CmDest.Name));

                CreateDataFlowTask(PackageName, Db2builder["Password"].ToString(), Stagingbuilder["Password"].ToString());

                Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();
                app.SaveToSqlServer(package, null, _server, _userid, _password);

            }
            catch (Exception e)
            {
                MessageBox.Show("Exception" + e.Message + e.StackTrace);
                throw new System.Exception("Error occurred on worker thread", e);
            }
        }

        private void CreateDataFlowTask(string PackageName, string SourcePwd, string DestPwd)
        {
            package = App.LoadFromSqlServer(PackageName, _server, _userid, _password, null);

            Executable exec = package.Executables.Add("STOCK:PipelineTask");
            TaskHost dataFlowTaskHost = exec as TaskHost;

            dataFlowTaskHost.Name = _Tablename;
            dataFlowTaskHost.FailPackageOnFailure = true;
            dataFlowTaskHost.FailParentOnFailure = true;
            dataFlowTaskHost.DelayValidation = true;
            
            MainPipe dataFlowTask = dataFlowTaskHost.InnerObject as MainPipe;

            ComponentEventHandler events = new ComponentEventHandler();
             
            dataFlowTask.Events = Microsoft.SqlServer.Dts.Runtime.DtsConvert.GetExtendedInterface(events as Microsoft.SqlServer.Dts.Runtime.IDTSComponentEvents);
            ConnectionManager CmSrc  = package.Connections[_CMISeries];  // Source             
            ConnectionManager CmDest = package.Connections[_CMStaging];  // Destination  

            CmSrc.DelayValidation = true;
            CmDest.DelayValidation = true;

            CmSrc.ConnectionString += "Password=" + SourcePwd + ";Network Transport Library=TCP;";
            CmDest.ConnectionString += "Password=" + DestPwd;
            
            // Create an OLE DB source to the data flow.
            Microsoft.SqlServer.Dts.Pipeline.Wrapper.IDTSComponentMetaData100 DataFlowSource = dataFlowTask.ComponentMetaDataCollection.New();
            DataFlowSource.ComponentClassID = "DTSAdapter.OleDbSource.2";
  
            CManagedComponentWrapper SrcInstance = DataFlowSource.Instantiate();  
            SrcInstance.ProvideComponentProperties();
 

            // Specify the Source connection manager.
            DataFlowSource.RuntimeConnectionCollection[0].ConnectionManager = DtsConvert.GetExtendedInterface(CmSrc);
            DataFlowSource.RuntimeConnectionCollection[0].ConnectionManagerID = CmSrc.ID;

            SrcInstance.SetComponentProperty("DefaultCodePage", _CodePage);
            SrcInstance.SetComponentProperty("AlwaysUseDefaultCodePage", true);

            DataFlowSource.Name = _CMISeries + _Tablename;            
            DataFlowSource.ValidateExternalMetadata = _ValidateExternalMetadata;
            
            

            // Set the custom properties. 
            SrcInstance.SetComponentProperty("AccessMode", 0);  // 0=TableName,1=variable tablename,2=sql command,3=           
            SrcInstance.SetComponentProperty("OpenRowset", _Tablename  ); //0=OpenRowset,(dbo.TableName);1=OpenRowsetVariable,(User::test);2=SqlCommand,(Select * from Product);
            SrcInstance.AcquireConnections(null);
            SrcInstance.ReinitializeMetaData();
            SrcInstance.ReleaseConnections();
         
            //Create  Destination Ole  
            IDTSComponentMetaData100 DataFlowDest = dataFlowTask.ComponentMetaDataCollection.New();
            DataFlowDest.ComponentClassID = "DTSAdapter.OleDbDestination.2";           
            CManagedComponentWrapper DestInstance = DataFlowDest.Instantiate(); // Get the design time instance of the component           
            DestInstance.ProvideComponentProperties(); // Initialize the component
            DataFlowDest.Name = _CMStaging+_Tablename;

            // Specify the connection manager.
            DataFlowDest.RuntimeConnectionCollection[0].ConnectionManagerID = CmDest.ID;
            DataFlowDest.RuntimeConnectionCollection[0].ConnectionManager = DtsConvert.GetExtendedInterface(CmDest);

            DestInstance.SetComponentProperty("DefaultCodePage", _CodePage);
            DestInstance.SetComponentProperty("AlwaysUseDefaultCodePage", true);
            
            DestInstance.SetComponentProperty("AccessMode", 3);// Set the custom properties. 3            
            DestInstance.SetComponentProperty("OpenRowset", _Tablename  );
            DataFlowDest.ValidateExternalMetadata = _ValidateExternalMetadata;

            // Reinitialize the metadata.
            DestInstance.AcquireConnections(null);
            DestInstance.ReinitializeMetaData();
            DestInstance.ReleaseConnections();

            
            // Create the path from DataFlow Source to destination.
            try
            {
                IDTSPath100 pathOleSource_Dest = dataFlowTask.PathCollection.New();
                pathOleSource_Dest.AttachPathAndPropagateNotifications(DataFlowSource.OutputCollection[0], DataFlowDest.InputCollection[0]);

                IDTSInput100 input = DataFlowDest.InputCollection[0];
                IDTSVirtualInput100 vInput = input.GetVirtualInput();

                foreach (IDTSVirtualInputColumn100 vColumn in vInput.VirtualInputColumnCollection)
                {
                    
                    IDTSInputColumn100 inputColumn = DestInstance.SetUsageType(input.ID, vInput, vColumn.LineageID, DTSUsageType.UT_READWRITE); // Select column, and retain new input column                    
                    IDTSExternalMetadataColumn100 externalColumn = input.ExternalMetadataColumnCollection[inputColumn.Name]; // Find external column by name                    
                    DestInstance.MapInputColumn(input.ID, inputColumn.ID, externalColumn.ID); // Map input column to external column
                    inputColumn.ExternalMetadataColumnID = externalColumn.ID;
                    externalColumn.Name = inputColumn.Name;
                   
                    if (inputColumn.DataType == Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType.DT_DECIMAL) 
                       {externalColumn.DataType = Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType.DT_R8 ; }
                    else {externalColumn.DataType =inputColumn.DataType;}
                    externalColumn.Length = inputColumn.Length;

                     
                }
            }
            catch (System.Runtime.InteropServices.COMException ce)
            {

                MessageBox.Show(string.Format("OLEDBDest:{0}: ErrorCodeIDTSPath100: {1}: Source {2}: ObjectName:{3} ",
                            DataFlowSource.GetErrorDescription(ce.ErrorCode),
                             ce.Message, CmSrc.Name,  " " ));
                throw new System.Exception("Error occurred on worker thread", ce);
            }

        }
        
        private string ReadSSISConfig ( string appValue  )
        {
            try
            {
                return ConfigurationManager.AppSettings[appValue];             
            }
            catch (Exception g)
            {  
             
                MessageBox.Show( g.Source + g.StackTrace + " " + " " + g.Message );
                return null;
            }

        }



        private TaskHost CreateExecuteSQLTask(Package package, string name, string ConnectionID, String sqlstatementsource, int CdPg)
        {
            try
            {

                TaskHost tkSQLTask1 = package.Executables.Add("STOCK:SQLTask") as TaskHost;
                ExecuteSQLTask executeSQLTask = tkSQLTask1.InnerObject as ExecuteSQLTask;

                tkSQLTask1.Name = name;
                tkSQLTask1.Description = name;
                tkSQLTask1.Properties["ResultSetType"].SetValue(tkSQLTask1, ResultSetType.ResultSetType_None);
                tkSQLTask1.Properties["Connection"].SetValue(tkSQLTask1, ConnectionID);
                tkSQLTask1.Properties["SqlStatementSourceType"].SetValue(tkSQLTask1, SqlStatementSourceType.DirectInput);
                tkSQLTask1.Properties["SqlStatementSource"].SetValue(tkSQLTask1, sqlstatementsource);
                tkSQLTask1.Properties["IsStoredProcedure"].SetValue(tkSQLTask1, false);
                tkSQLTask1.Properties["BypassPrepare"].SetValue(tkSQLTask1, false);
                executeSQLTask.CodePage = (uint)CdPg;

                return tkSQLTask1;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return null;
            }
        }
        
        private void GetSQLTableSchema(Popup form2, string tablename, string ConStr, string SQLPwd)
        {
              
            OleDbConnectionStringBuilder ConStrPrd = new OleDbConnectionStringBuilder();
            ConStrPrd.ConnectionString = (ConStr);
            ConStrPrd["password"] = SQLPwd;
            ConStrPrd.Remove("provider");

            string cmdtext = " SELECT A.name FROM sys.columns A INNER JOIN sys.tables B ON A.Object_id=B.Object_id WHERE B.name = '" + _Tableprefix + _Tablename.Substring(2) + "' ORDER BY 1";
                    
            using (var conn = new SqlConnection(ConStrPrd.ConnectionString))
            {
                using (var cmd = new SqlCommand(cmdtext, conn) { CommandType = CommandType.Text })
                {
                    conn.Open();
                    SqlDataReader dreader = cmd.ExecuteReader();
                    Popup Popform = new Popup();
                    
                    while (dreader.Read())
                    {                  
                        form2.populatePopupCheckListbox(dreader["name"].ToString());                
                    }                 
                    conn.Close();
                }
            }
    


        }

        private string GetSSISConnectionString(string connectionManager )
        {
            try
            {
                OleDbConnectionStringBuilder ConStrPrd = new OleDbConnectionStringBuilder();
                ConStrPrd.ConnectionString = (ConstructConnectionString());
                var cmdtext = " SELECT ConfiguredValue FROM " + _SSISConfiguration + "  WHERE PackagePath LIKE '%" + connectionManager + "%' ";
                string str = null;

                using (var conn = new SqlConnection(ConStrPrd.ConnectionString))
                {
                    using (var command = new SqlCommand(cmdtext, conn) { CommandType = CommandType.Text })
                    {
                        conn.Open();
                        str = (string)command.ExecuteScalar();
                        conn.Close();
                    }
                }

                return str;
            }
            catch(Exception e)
            {                
                MessageBox.Show(e.Message + " " + e.Source + " " + e.StackTrace);
                return null;
            }
        }

        private Boolean BackupSSISPackages( )
        {

            try
            {

                string rootdrive = Path.GetPathRoot(Environment.CurrentDirectory); 
                string path = Environment.CurrentDirectory;

                string n = string.Format("-{0:yyyyMMddhhmmsstt}", DateTime.Now, Path.DirectorySeparatorChar);
                
                var tmppath = Path.Combine(path, "Backup");
                                
                tmppath = Path.Combine(tmppath,   _server + n);
                if (!Directory.Exists(tmppath))
                {
                    
                    Directory.CreateDirectory(tmppath);
                }
             


                for (int x = 0; x <= checkedListBox1.CheckedItems.Count - 1; x++)
                {
                    Package pkg = App.LoadFromSqlServer(checkedListBox1.CheckedItems[x].ToString(), _server, _userid, _password, null);

                    Microsoft.SqlServer.Dts.Runtime.Application app = new Microsoft.SqlServer.Dts.Runtime.Application();

                    app.SaveToXml(tmppath+"\\"+pkg.Name+".dtsx" , pkg, null);
                }

                return true;

            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message);
                return false;
            }

           
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            char arg = (char)e.Argument;

            switch (arg)
            {
                case 'F': Generate_New_Field();
                    break;
                case 'T': Generate_New_Table();
                    break;
            }
            
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                this.pictureBox1.Image = SSIS_DataFlow.Properties.Resources.CloseError;
                this.toolStripStatusLabel2.Text = "Error";
            }
            else 
            {
                this.pictureBox1.Image = SSIS_DataFlow.Properties.Resources.check_mark;
                this.toolStripStatusLabel2.Text = "Success";
            }
        }

 

      

    }
}
