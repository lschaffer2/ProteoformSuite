﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace PS_0._00
{
    public partial class TheoreticalDatabase : Form
    {
        OpenFileDialog openFileDialog2 = new OpenFileDialog();
        OpenFileDialog openFileDialog3 = new OpenFileDialog();
        DataGridView dgv_database = new DataGridView();
        protein[] proteinRawInfo = null;
        int totalNumEntries = 0;

        struct protein
        {
            public string accession, name, fragment, sequence;
            public int begin, end;
            public Dictionary<int, List<string>> positionsAndPtms;
        }

        public TheoreticalDatabase()
        {
            InitializeComponent();
        }

        private void FillDataBaseTable(string table)
        {
            BindingSource dgv_DB_BS = new BindingSource();
            dgv_DB_BS.DataSource = GlobalData.theoreticalAndDecoyDatabases.Tables[table];
            dgv_Database.DataSource = dgv_DB_BS;
            dgv_database.AutoGenerateColumns = true;
            dgv_database.DefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
            dgv_database.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.DarkGray;
        }

        private void btn_GetUniProtXML_Click(object sender, EventArgs e)
        {
            DialogResult dr = this.openFileDialog2.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                String uniprotXmlFile = openFileDialog2.FileName;
                try
                {
                    tb_UniProtXML_Path.Text = uniprotXmlFile;
                    totalNumEntries = NumberOfUniProtEntrys(uniprotXmlFile);
                    proteinRawInfo = new protein[totalNumEntries];
                    proteinRawInfo = GetProteinRawInfo(uniprotXmlFile, totalNumEntries);
                }
                catch (SecurityException ex)
                {
                    // The user lacks appropriate permissions to read files, discover paths, etc.
                    MessageBox.Show("Security error. Please contact your administrator for details.\n\nError message: " + ex.Message + "\n\n" +
                        "Details (send to Support):\n\n" + ex.StackTrace);
                    tb_UniProtXML_Path.Text = "";
                }
                catch (Exception ex)
                {
                    // Could not load the result file - probably related to Windows file system permissions.
                    MessageBox.Show("Cannot display the file: " + uniprotXmlFile.Substring(uniprotXmlFile.LastIndexOf('\\'))
                        + ". You may not have permission to read the file, or it may be corrupt.\n\nReported error: " + ex.Message);
                    tb_UniProtXML_Path.Text = "";
                }
            }
        }

        private void TheoreticalDatabase_Load(object sender, EventArgs e)
        {
            InitializeOpenFileDialog2();
            InitializeOpenFileDialog3();
            InitializeSettings();
        }

        private void InitializeSettings()
        {
            ckbx_OxidMeth.Checked = false;
            ckbx_Carbam.Checked = true;
            ckbx_Meth_Cleaved.Checked = true;

            btn_NeuCode_Lt.Checked = true;

            nUD_MaxPTMs.Minimum = 0;
            nUD_MaxPTMs.Maximum = 5;
            nUD_MaxPTMs.Value = 3;

            nUD_NumDecoyDBs.Minimum = 0;
            nUD_NumDecoyDBs.Maximum = 50;
            nUD_NumDecoyDBs.Value = 0;
        }

        private void InitializeOpenFileDialog2()
        {
            // Set the file dialog to filter for graphics files.
            this.openFileDialog2.Filter = "UniProt XML (*.xml)|*.xml";
            // Allow the user to select multiple images.
            this.openFileDialog2.Multiselect = false;
            this.openFileDialog2.Title = "UniProt XML Format Database";
        }

        private void InitializeOpenFileDialog3()
        {
            // Set the file dialog to filter for graphics files.
            this.openFileDialog3.Filter = "UniProt PTM List (*.txt)|*.txt";
            // Allow the user to select multiple images.
            this.openFileDialog3.Multiselect = false;
            this.openFileDialog3.Title = "UniProt PTM List";
        }

        private void btn_UniPtPtmList_Click(object sender, EventArgs e)
        {
            DialogResult dr = this.openFileDialog3.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                String file = openFileDialog3.FileName;
                try
                {
                    tb_UniProtPtmList_Path.Text = file;
                }
                catch (SecurityException ex)
                {
                    // The user lacks appropriate permissions to read files, discover paths, etc.
                    MessageBox.Show("Security error. Please contact your administrator for details.\n\nError message: " + ex.Message + "\n\n" +
                        "Details (send to Support):\n\n" + ex.StackTrace);
                }
                catch (Exception ex)
                {
                    // Could not load the result file - probably related to Windows file system permissions.
                    MessageBox.Show("Cannot display the file: " + file.Substring(file.LastIndexOf('\\'))
                        + ". You may not have permission to read the file, or it may be corrupt.\n\nReported error: " + ex.Message);
                }

            }
        }

        private void btn_Make_Databases_Click(object sender, EventArgs e)
        {

            Dictionary<char, double> aaIsotopeMassList = new Dictionary<char, double>();
            string kI = WhichLysineIsotopeComposition(); 
            aaIsotopeMassList = AminoAcidMasses.GetAA_Masses(Convert.ToBoolean(ckbx_OxidMeth.Checked), Convert.ToBoolean(ckbx_Carbam.Checked), kI);
            
            ProteomeDatabaseReader rup = new ProteomeDatabaseReader();
            ProteomeDatabaseReader.oldPtmFilePath = tb_UniProtPtmList_Path.Text; // gets the exising path to PTM list into read_uniprot_ptlist class
            Dictionary<string, Modification> uniprotModificationTable = new Dictionary<string, Modification>();
            uniprotModificationTable = rup.ReadUniprotPtmlist();

            string giantProtein = GetOneGiantProtein(tb_UniProtXML_Path.Text, Convert.ToBoolean(ckbx_Meth_Cleaved.Checked));
            processEntries(proteinRawInfo, Convert.ToBoolean(ckbx_Meth_Cleaved.Checked), totalNumEntries, aaIsotopeMassList, Convert.ToInt32(nUD_MaxPTMs.Value), uniprotModificationTable);
            processDecoys(Convert.ToInt32(nUD_NumDecoyDBs.Value), giantProtein, proteinRawInfo, Convert.ToBoolean(ckbx_Meth_Cleaved.Checked), totalNumEntries, aaIsotopeMassList, Convert.ToInt32(nUD_MaxPTMs.Value), uniprotModificationTable);

            BindingList<string> bindinglist = new BindingList<string>();
            BindingSource bSource = new BindingSource();
            bSource.DataSource = bindinglist;
            cmbx_DisplayWhichDB.DataSource = bSource;

            foreach (DataTable dt in GlobalData.theoreticalAndDecoyDatabases.Tables)
            {
                bindinglist.Add(dt.TableName);
                //cmbx_DisplayWhichDB.Items.Add(dt.TableName[0].ToString());
            }

            FillDataBaseTable(cmbx_DisplayWhichDB.SelectedItem.ToString());
        }

        static DataTable GenerateProteoformDatabaseDataTable(string title)
        {
            DataTable dt = new DataTable(title);//datatable name goes in parentheses.
            dt.Columns.Add("Accession", typeof(string));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Fragment", typeof(string));
            dt.Columns.Add("Begin", typeof(int));
            dt.Columns.Add("End", typeof(int));
            dt.Columns.Add("Mass", typeof(double));
            dt.Columns.Add("Lysine Count", typeof(int));
            dt.Columns.Add("PTM List", typeof(string));
            dt.Columns.Add("PTM Group Mass", typeof(double));
            dt.Columns.Add("Proteoform Mass", typeof(double));
            return dt;
        }

        static void processDecoys(int numDb, string giantProtein, protein[] pRD, bool mC, int num, Dictionary<char, double> aaIsotopeMassList, 
            int maxPTMsPerProteoform, Dictionary<string, Modification> uniprotModificationTable)
        {

            for (int decoyNumber = 0; decoyNumber < numDb; decoyNumber++)
            {

                DataTable decoy = GenerateProteoformDatabaseDataTable("DecoyDatabase_" + decoyNumber);

                new Random().Shuffle(pRD); //Randomize Order of Protein Array

                for (int i = 0; i < num; i++)
                {
                    bool isMetCleaved = (mC && pRD[i].begin == 0 && pRD[i].sequence.Substring(0, 1) == "M"); // methionine cleavage of N-terminus specified
                    int startPosAfterCleavage = Convert.ToInt32(isMetCleaved);

                    //From the concatenated proteome, cut a decoy sequence of a randomly selected length
                    int hunkLength = pRD[i].sequence.Length - startPosAfterCleavage;
                    string hunk = giantProtein.Substring(0, hunkLength);
                    giantProtein.Remove(0, hunkLength);
                    EnterTheoreticalProteformFamily(decoy, hunk, pRD[i], pRD[i].accession + "_DECOY_" + decoyNumber, maxPTMsPerProteoform, isMetCleaved, aaIsotopeMassList, uniprotModificationTable);
                }

                GlobalData.theoreticalAndDecoyDatabases.Tables.Add(decoy);
            }
        }

        static void processEntries(protein[] pRD, bool mC, int num, Dictionary<char, double> aaIsotopeMassList, 
            int maxPTMsPerProteoform, Dictionary<string, Modification> uniprotModificationTable)
        {

            DataTable target = GenerateProteoformDatabaseDataTable("target");

            for (int i = 0; i < num; i++)
            {
                bool isMetCleaved = (mC && pRD[i].begin == 0 && pRD[i].sequence.Substring(0, 1) == "M"); // methionine cleavage of N-terminus specified
                int startPosAfterCleavage = Convert.ToInt32(isMetCleaved);
                string seq = pRD[i].sequence.Substring(startPosAfterCleavage, (pRD[i].sequence.Length - startPosAfterCleavage));
                EnterTheoreticalProteformFamily(target, seq, pRD[i], pRD[i].accession, maxPTMsPerProteoform, isMetCleaved, aaIsotopeMassList, uniprotModificationTable);
            }

            GlobalData.theoreticalAndDecoyDatabases.Tables.Add(target);
        }

        static void EnterTheoreticalProteformFamily(DataTable table, string seq, protein prot, string accession, int maxPTMsPerProteoform, bool isMetCleaved,
            Dictionary<char, double> aaIsotopeMassList, Dictionary<string, Modification> uniprotModificationTable)
        {
            //Calculate the properties of this sequence
            double mass = CalculateProteoformMass(ref aaIsotopeMassList, seq);
            int kCount = seq.Split('K').Length - 1;

            //Initialize a PTM combination list with "unmodified," and then add other PTMs 
            List<OneUniquePtmGroup> aupg = new List<OneUniquePtmGroup>(new OneUniquePtmGroup[] { new OneUniquePtmGroup(0, new List<string>(new string[] { "unmodified" })) });
            bool addPtmCombos = maxPTMsPerProteoform > 0 && prot.positionsAndPtms.Count() > 0;
            if (addPtmCombos)
            {
                aupg.AddRange(new PtmCombos().combos(maxPTMsPerProteoform, uniprotModificationTable, prot.positionsAndPtms));
            }

            foreach (OneUniquePtmGroup group in aupg)
            {
                List<string> ptm_list = group.unique_ptm_combinations;
                //if (!isMetCleaved) { MessageBox.Show("PTM Combinations: " + String.Join("; ", ptm_list)); }
                Double ptm_mass = group.mass;
                Double proteoform_mass = mass + group.mass;
                table.Rows.Add(accession, prot.name, prot.fragment, prot.begin + Convert.ToInt32(isMetCleaved), prot.end, mass, kCount, string.Join("; ", ptm_list), ptm_mass, proteoform_mass);
            }
        }

        static double CalculateProteoformMass(ref Dictionary<char, double> aaIsotopeMassList, string pForm)
        {
            double proteoformMass = 18.010565; // start with water
            char[] aminoAcids = pForm.ToCharArray();
            for (int i = 0; i < pForm.Length; i++)
            {
                double aMass = 0;
                try
                {
                    aMass = aaIsotopeMassList[aminoAcids[i]];
                }
                catch
                {
                    MessageBox.Show("Did not recognize amino acid " + aminoAcids[i] + " while calculating the mass.\nThis will be recorded as mass = 0.");
                    aMass = 0;
                }
                proteoformMass = proteoformMass + aMass;
            }

            return proteoformMass;
        }

        static protein[] GetProteinRawInfo(string uniprotXmlFile, int num)
        {
            protein[] pRI = new protein[num];

            using (FileStream xmlStream = new FileStream(uniprotXmlFile, FileMode.Open))
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                using (XmlReader xmlReader = XmlReader.Create(xmlStream, settings))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(uniprot));
                    uniprot deserializeduniprot = serializer.Deserialize(xmlReader) as uniprot;
                    int count = 0;
                    foreach (var _entry in deserializeduniprot.entry)
                    {
                        Dictionary<int, List<string>> pAP = new Dictionary<int, List<string>>();

                        //Get the positions and descriptions of PTMs annotated for this entry; only if they are certain!
                        pAP = GetPositionsPTMs(_entry); // dictionary of positions and PTMs in complete entry

                        pRI[count].accession = _entry.accession[0];
                        //MessageBox.Show(_entry.accession[0]);
                        pRI[count].name = GetProteinName(_entry);
                        pRI[count].fragment = "full";

                        //this next bit eliminates return characters etc from sequence string
                        string fullSequence = _entry.sequence.Value;
                        char[] arr = fullSequence.ToCharArray();
                        arr = Array.FindAll<char>(arr, (c => (char.IsLetter(c))));
                        fullSequence = new string(arr);

                        pRI[count].sequence = fullSequence;
                        pRI[count].begin = 0;
                        pRI[count].end = fullSequence.Length - 1;
                        int fullSequenceLength = _entry.sequence.length;
                        pRI[count].positionsAndPtms = SegmentPTMs(pAP, pRI[count].begin, pRI[count].end);

                        count++;

                        foreach (var proteinFeature in _entry.feature)//process protein fragments
                        {
                            string type = proteinFeature.type.ToString();

                            int position = 0;
                            int begin = 0;
                            int end = 0;
                            bool realFeature = false;

                            int noPosition = 0;
                            try
                            {
                                if (proteinFeature.location.ItemsElementName[0].ToString() != "position")
                                {
                                    noPosition = 1;
                                }
                            }
                            catch
                            {
                                noPosition = 0;
                            }
                            if (noPosition == 1) // has begin and end
                            {
                                if (proteinFeature.location.Items[0].status.ToString() == "certain"
                                    && proteinFeature.location.Items[0].positionSpecified)
                                {
                                    realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out begin);
                                }
                                if (realFeature)
                                {
                                    if (proteinFeature.location.Items[1].status.ToString() == "certain"
                                        && proteinFeature.location.Items[1].positionSpecified)
                                    {
                                        realFeature = int.TryParse(proteinFeature.location.Items[1].position.ToString(), out end);
                                    }
                                }

                                if (realFeature)
                                {
                                    begin = begin - 1;
                                    end = end - 1;
                                    //MessageBox.Show("parse b: " + begin + "end: " + end);
                                    if ((begin < 0) || (end < 0))
                                    {
                                        realFeature = false;
                                    }
                                }
                            }
                            else // protein only as single position location
                            {
                                if (proteinFeature.location.Items[0].status.ToString() == "certain"
                                    && proteinFeature.location.Items[0].positionSpecified)
                                {
                                    realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out position);
                                    if (realFeature)
                                    {
                                        position = position - 1;
                                        if (position < 0)
                                        {
                                            realFeature = false;
                                        }
                                    }
                                }
                            }
                            if (realFeature)
                            {
                                switch (type)
                                {
                                    case "signalpeptide"://spaces are sometimes deleted in xml read
                                        pRI[count].accession = _entry.accession[0];
                                        pRI[count].name = GetProteinName(_entry);
                                        pRI[count].fragment = "signal peptide";
                                        pRI[count].sequence = fullSequence.Substring(begin, (end - begin + 1));
                                        pRI[count].begin = begin;
                                        pRI[count].end = end;
                                        pRI[count].positionsAndPtms = SegmentPTMs(pAP, begin, end);
                                        count++;
                                        break;
                                    case "chain":
                                        if ((end - begin + 1) != fullSequenceLength)
                                        {
                                            pRI[count].accession = _entry.accession[0];
                                            pRI[count].name = GetProteinName(_entry);
                                            pRI[count].fragment = "chain";
                                            pRI[count].sequence = fullSequence.Substring(begin, (end - begin + 1));
                                            pRI[count].begin = begin;
                                            pRI[count].end = end;
                                            pRI[count].positionsAndPtms = SegmentPTMs(pAP, begin, end);
                                            count++;
                                        }
                                        break;
                                    case "propeptide":
                                        pRI[count].accession = _entry.accession[0];
                                        pRI[count].name = GetProteinName(_entry);
                                        pRI[count].fragment = "propeptide";
                                        pRI[count].sequence = fullSequence.Substring(begin, (end - begin) + 1);
                                        pRI[count].begin = begin;
                                        pRI[count].end = end;
                                        pRI[count].positionsAndPtms = SegmentPTMs(pAP, begin, end);
                                        count++;
                                        break;
                                    case "peptide":
                                        pRI[count].accession = _entry.accession[0];
                                        pRI[count].name = GetProteinName(_entry);
                                        pRI[count].fragment = "peptide";
                                        pRI[count].sequence = fullSequence.Substring(begin, (end - begin) + 1);
                                        pRI[count].begin = begin;
                                        pRI[count].end = end;
                                        pRI[count].positionsAndPtms = SegmentPTMs(pAP, begin, end);
                                        count++;
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            return pRI;
        }

        static Dictionary<int, List<string>> SegmentPTMs(Dictionary<int, List<string>> allPosPTMs, int begin, int end)
        {
            Dictionary<int, List<string>> segPosPTMs = new Dictionary<int, List<string>>();

            foreach (int position in allPosPTMs.Keys)
            {
                if (position >= begin && position <= end)
                {
                    segPosPTMs.Add(position, allPosPTMs[position]);
                }
            }

            return segPosPTMs;// the int is the amino acid position and the string[] are the different ptms at that position
        }

        static string GetProteinName(entry _ent)
        {
            string name = "";

            int proteinNameType = 1;
            try
            {
                if ((_ent.protein.recommendedName.fullName.Value) != null)
                {
                    proteinNameType = 1;
                }
            }
            catch
            {
                try
                {
                    if ((_ent.protein.submittedName[0].fullName.Value) != null)
                    {
                        proteinNameType = 2;
                    }
                }
                catch
                {
                    proteinNameType = 3;
                }
            }


            switch (proteinNameType)
            {
                case 1: //Recommended Name
                    name = _ent.protein.recommendedName.fullName.Value;
                    break;
                case 2: //Submitted Name
                    name = _ent.protein.submittedName[0].fullName.Value.ToString();
                    break;
                case 3: //Alternative Name
                    name = _ent.protein.alternativeName[0].fullName.Value.ToString();
                    break;
                default:
                    name = "";
                    break;
            }

            return name;
        }

        static Dictionary<int, List<string>> GetPositionsPTMs(entry _ent)
        {
            Dictionary<int, List<string>> local_pAP = new Dictionary<int, List<string>>();

            //Check out each feature in the list of features for this entry
            foreach (var proteinFeature in _ent.feature)//process protein ptms
            {
                string type = proteinFeature.type.ToString();

                int position = 0;
                int begin = 0;
                int end = 0;
                bool realFeature = false;

                int noPosition = 0;
                try
                {
                    if (proteinFeature.location.ItemsElementName[0].ToString() != "position") { noPosition = 1; }
                }
                catch
                {
                    noPosition = 0;
                }
                if (noPosition == 1) // has begin and end
                {
                    if (proteinFeature.location.Items[0].status.ToString() == "certain"
                        && proteinFeature.location.Items[0].positionSpecified)
                    {
                        realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out begin);
                        if (realFeature)
                        {
                            if (proteinFeature.location.Items[1].status.ToString() == "certain"
                                && proteinFeature.location.Items[1].positionSpecified)
                            {
                                realFeature = int.TryParse(proteinFeature.location.Items[1].position.ToString(), out end);
                            }
                        }
                    }
                    if (realFeature)
                    {
                        begin = begin - 1;
                        end = end - 1;
                        if ((begin < 0) || (end < 0))
                        {
                            realFeature = false;
                        }
                    }
                }
                else // protein only has single position location
                {
                    if (proteinFeature.location.Items[0].status.ToString() == "certain"
                        && proteinFeature.location.Items[0].positionSpecified)
                    {
                        realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out position);
                        if (realFeature)
                        {
                            position = position - 1;
                            if (position < 0)
                            {
                                realFeature = false;
                            }
                        }
                    }
                }

                if (realFeature)
                {
                    switch (type)
                    {
                        case "modifiedresidue":
                            string description = proteinFeature.description.ToString();
                            //MessageBox.Show(_ent.accession[0] + "\t" + description + "\t" + position);
                            if (local_pAP.ContainsKey(position))
                            {
                                List<string> morePtms = new List<string>();
                                morePtms = local_pAP[position].ToList();
                                morePtms.Add(description.Split(';')[0]);//take description up to ';' if there is one
                                local_pAP[position] = morePtms;
                            }
                            else
                            {
                                List<string> ptms = new List<string>();
                                ptms.Add(description.Split(';')[0]);
                                local_pAP.Add(position, ptms);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            return local_pAP;
        }

        static string GetOneGiantProtein(string inFile, bool mC)//returns sum of full length, signal peptide, chain, propetide and peptide
        {
            StringBuilder giantProtein = new StringBuilder(5000000); // this set-aside is autoincremented to larger values when necessary.

            using (FileStream xmlStream = new FileStream(inFile, FileMode.Open))
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                using (XmlReader xmlReader = XmlReader.Create(xmlStream, settings))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(uniprot));
                    uniprot deserializeduniprot = serializer.Deserialize(xmlReader) as uniprot;
                    foreach (var _entry in deserializeduniprot.entry)
                    {
                        string thisFullSequence = _entry.sequence.Value.Replace("\r", "").Replace("\n", "");
                        //MessageBox.Show(thisFullSequence.ToString());

                        bool isMetCleaved = mC && (thisFullSequence.Substring(0, 1) == "M");
                        int startPosAfterMetCleavage = Convert.ToInt32(isMetCleaved);
                        giantProtein.Append("-" + thisFullSequence.Substring(startPosAfterMetCleavage));
                       
                        int fullSequenceLength = thisFullSequence.Length;

                        foreach (var proteinFeature in _entry.feature) //process protein fragments
                        {
                            string type = proteinFeature.type.ToString();

                            int position = 0;
                            int begin = 0;
                            int end = 0;
                            bool realFeature = false;

                            int noPosition = 0;
                            try
                            {
                                if (proteinFeature.location.ItemsElementName[0].ToString() != "position") { noPosition = 1; }
                            }
                            catch
                            {
                                noPosition = 0;
                            }
                            if (noPosition == 1) // has begin and end
                            {
                                if (proteinFeature.location.Items[0].status.ToString() == "certain"
                                    && proteinFeature.location.Items[0].positionSpecified)
                                {
                                    realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out begin);
                                }
                                if (realFeature)
                                {
                                    if (proteinFeature.location.Items[1].status.ToString() == "certain"
                                        && proteinFeature.location.Items[1].positionSpecified)
                                    {
                                        realFeature = int.TryParse(proteinFeature.location.Items[1].position.ToString(), out end);
                                    }
                                }

                                if (realFeature)
                                {
                                    begin = begin - 1;
                                    end = end - 1;
                                    //MessageBox.Show("parse b: " + begin + "end: " + end);
                                    if ((begin < 0) || (end < 0))
                                    {
                                        realFeature = false;
                                    }
                                }
                            }
                            else // protein only as single position location
                            {
                                if (proteinFeature.location.Items[0].status.ToString() == "certain"
                                    && proteinFeature.location.Items[0].positionSpecified)
                                {
                                    realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out position);
                                    if (realFeature)
                                    {
                                        position = position - 1;
                                        if (position < 0)
                                        {
                                            realFeature = false;
                                        }
                                    }
                                }
                            }
                            if (realFeature)
                            {
                                if (mC && (begin == 0) && end >= 1 && (thisFullSequence.Substring(0, 1) == "M"))
                                {
                                    //MessageBox.Show("inside begin + 1");
                                    begin = begin + 1;
                                }

                                switch (type)
                                {
                                    case "signalpeptide"://spaces are sometimes deleted in xml read
                                        giantProtein.Append("." + thisFullSequence.Substring(begin, (end - begin + 1)));
                                        //MessageBox.Show(thisFullSequence.Substring(begin, (end - begin + 1)));
                                        break;
                                    case "chain":
                                        if (mC == true)
                                        {
                                            if ((end - begin + 1) != (fullSequenceLength - 1))
                                            {
                                                giantProtein.Append("." + thisFullSequence.Substring(begin, (end - begin + 1)));
                                            }
                                        }
                                        else
                                        {
                                            if ((end - begin + 1) != fullSequenceLength)
                                            {
                                                giantProtein.Append("." + thisFullSequence.Substring(begin, (end - begin + 1)));
                                            }
                                        }
                                        break;
                                    case "propeptide":
                                        giantProtein.Append("." + thisFullSequence.Substring(begin, (end - begin + 1)));
                                        break;
                                    case "peptide":
                                        giantProtein.Append("." + thisFullSequence.Substring(begin, (end - begin + 1)));
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            return giantProtein.ToString();
        }

        static int NumberOfUniProtEntrys(string inFile)//returns sum of full length, signal peptide, chain, propetide and peptide
        {
            var nodeCount = 0;

            using (FileStream xmlStream = new FileStream(inFile, FileMode.Open))
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                using (XmlReader xmlReader = XmlReader.Create(xmlStream, settings))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(uniprot));
                    uniprot deserializeduniprot = serializer.Deserialize(xmlReader) as uniprot;
                    foreach (var _entry in deserializeduniprot.entry)
                    {
                        nodeCount++;//one for main entry
                        int fullSequenceLength = _entry.sequence.length;

                        foreach (var proteinFeature in _entry.feature)//process protein fragments
                        {
                            string type = proteinFeature.type.ToString();

                            int position = 0;
                            int begin = 0;
                            int end = 0;
                            bool realFeature = false;

                            int noPosition = 0;
                            try
                            {
                                if (proteinFeature.location.ItemsElementName[0].ToString() != "position") { noPosition = 1; }
                            }
                            catch
                            {
                                noPosition = 0;
                            }
                            if (noPosition == 1) // has begin and end
                            {
                                if (proteinFeature.location.Items[0].status.ToString() == "certain"
                                    && proteinFeature.location.Items[0].positionSpecified)
                                {
                                    realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out begin);
                                }
                                if (realFeature)
                                {
                                    if (proteinFeature.location.Items[1].status.ToString() == "certain"
                                        && proteinFeature.location.Items[1].positionSpecified)
                                    {
                                        realFeature = int.TryParse(proteinFeature.location.Items[1].position.ToString(), out end);
                                    }
                                }

                                if (realFeature)
                                {
                                    begin = begin - 1;
                                    end = end - 1;
                                    //MessageBox.Show("parse b: " + begin + "end: " + end);
                                    if ((begin < 0) || (end < 0))
                                    {
                                        realFeature = false;
                                    }
                                }
                            }
                            else // protein only as single position location
                            {
                                if (proteinFeature.location.Items[0].status.ToString() == "certain"
                                    && proteinFeature.location.Items[0].positionSpecified)
                                {
                                    realFeature = int.TryParse(proteinFeature.location.Items[0].position.ToString(), out position);
                                    if (realFeature)
                                    {
                                        position = position - 1;
                                        if (position < 0)
                                        {
                                            realFeature = false;
                                        }
                                    }
                                }
                            }
                            if (realFeature)
                            {
                                switch (type)
                                {
                                    case "signalpeptide"://spaces are sometimes deleted in xml read
                                        nodeCount++;
                                        break;
                                    case "chain":
                                        if ((end - begin + 1) != fullSequenceLength)
                                        {
                                            //MessageBox.Show("b: {0} e: {1} f: {2}", begin, end, fullSequenceLength);
                                            nodeCount++;
                                        }
                                        break;
                                    case "propeptide":
                                        nodeCount++;
                                        break;
                                    case "peptide":
                                        nodeCount++;
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            return nodeCount;
        }

        private string WhichLysineIsotopeComposition()
        {
            string kI;

            if (btn_NaturalIsotopes.Checked)
            {
                kI = "n";
            }
            else if (btn_NeuCode_Lt.Checked)
            {
                kI = "l";
            }
            else  // must be heavy neucode (aka btn_NeuCode_Hv.Checked
            {
                kI = "h";
            }
            return kI;
        }

        //static bool ValidateUniProtXML(string testXmlFile)
        //{
        //    bool valid = false;
        //    string line1, line2;
        //    try
        //    {
        //        using (StreamReader reader = new StreamReader(testXmlFile))
        //        {
        //            line1 = reader.ReadLine();
        //            line2 = reader.ReadLine();
        //            reader.DiscardBufferedData();
        //            reader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);

        //            if (line2.Contains("uniprot"))
        //            {
        //                valid = true;
        //            }
        //            else
        //            {
        //                MessageBox.Show("This is not a valid uniprot .xml file. Try again.");
        //            }
        //        }
        //    }
        //    catch
        //    {
        //        MessageBox.Show("Try again.");
        //    }
        //    return valid;
        //}

        private void groupBox1_Enter(object sender, EventArgs e)
        {
            //don't know how to delete this
        }

        private void cmbx_DisplayWhichDB_SelectedIndexChanged(object sender, EventArgs e)
        {
            FillDataBaseTable(cmbx_DisplayWhichDB.SelectedItem.ToString());
        }
    }
}
