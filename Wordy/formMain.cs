﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.ServiceModel.Syndication;
using GenericForms;

namespace Wordy
{
    public partial class formMain : Form
    {
        List<Entry> words;
        public List<WordOfTheDay> wotds;
        public Preferences prefs;
        public bool needWotDCheck = true;


        void loadWords()
        {
            words = new List<Entry>();

            StreamReader fRdr = new StreamReader(Application.StartupPath + "\\words.txt");

            string line, word, defsTxt, synonyms, rhymes;
            DateTime created, learned, lastTest, nextTest;
            int learningPhase, nStudyAttempts, nRecallAttempts, nRecallSuccesses;
            bool archived;

            while (!fRdr.EndOfStream)
            {
                line = fRdr.ReadLine();
                while (line != "<entry>" && !fRdr.EndOfStream)
                    line = fRdr.ReadLine();
                if (fRdr.EndOfStream)
                    break;

                word = fRdr.ReadLine();

                line = fRdr.ReadLine();
                if (line != "[defs]")
                    throw new Exception("error while loading entries");

                defsTxt = "";
                line = fRdr.ReadLine();
                while (line != "[/defs]")
                {
                    defsTxt += line + Environment.NewLine;
                    line = fRdr.ReadLine();
                }

                defsTxt = defsTxt.Substring(0, defsTxt.Length - 2);
                synonyms = fRdr.ReadLine();
                rhymes = fRdr.ReadLine();

                created = DateTime.Parse(fRdr.ReadLine());
                learned = DateTime.Parse(fRdr.ReadLine());
                lastTest = DateTime.Parse(fRdr.ReadLine());
                nextTest = DateTime.Parse(fRdr.ReadLine());
                
                learningPhase = int.Parse(fRdr.ReadLine());
                nStudyAttempts = int.Parse(fRdr.ReadLine());
                nRecallAttempts = int.Parse(fRdr.ReadLine());
                nRecallSuccesses = int.Parse(fRdr.ReadLine());

                archived = bool.Parse(fRdr.ReadLine());

                line = fRdr.ReadLine();
                if (line != "</entry>")
                    throw new Exception("error while loading entries");

                words.Add(new Entry(word, new Definition(defsTxt), synonyms, rhymes, created, learned, lastTest, nextTest, learningPhase, nStudyAttempts, nRecallAttempts, nRecallSuccesses, archived));
            }

            fRdr.Close();
        }

        public void LoadSubs()
        {
            wotds = new List<WordOfTheDay>();

            StreamReader fRdr = new StreamReader(Application.StartupPath + "\\wotds.txt");
            while (!fRdr.EndOfStream)
                wotds.Add(new WordOfTheDay(fRdr.ReadLine(), fRdr.ReadLine(), bool.Parse(fRdr.ReadLine()), fRdr.ReadLine()));
            fRdr.Close();
        }

        public bool WordExists(string word)
        {
            return words.Any(e => e.ToString() == word);
        }
        
        public void SaveWords()
        {
            StreamWriter fWrtr = new StreamWriter(Application.StartupPath + "\\words.txt");

            foreach (Entry word in words)
                fWrtr.WriteLine(word.FormatEntry());

            fWrtr.Close();
        }

        public void SaveSubs()
        {
            StreamWriter fWrtr = new StreamWriter(Application.StartupPath + "\\wotds.txt");

            foreach (WordOfTheDay wotd in wotds)
                fWrtr.WriteLine(wotd.FormatWotD());

            fWrtr.Close();
        }

        public void AddNewWords(Dictionary<string, Definition> newWords, Dictionary<string, string> synonyms, Dictionary<string, string> rhymes)
        {
            foreach (KeyValuePair<string, Definition> word in newWords)
                words.Add(new Entry(word.Key, word.Value, Entry.FormatCommas(synonyms[word.Key]), rhymes[word.Key]));

            SaveWords();
        }

        public List<string> GetRandWords(int n, string exception)
        {
            List<string> wordsCopy = new List<string>(), randWords = new List<string>();

            foreach (Entry word in words)
                if (word.ToString() != exception)
                    wordsCopy.Add(word.ToString());

            Random rand = new Random((int)DateTime.Now.Ticks);

            for (int i = 0; i < n && wordsCopy.Count > 0; i++)
            {
                int ind = rand.Next(wordsCopy.Count);

                randWords.Add(wordsCopy[ind].ToString());
                wordsCopy.RemoveAt(ind);
            }

            //need more words?
            if (randWords.Count < n)
            {
                string[] constWords = { "defenestration", "palimpsest", "sagittipotent", "slayer", "rarity", "skald" };

                for (int i = randWords.Count; i < n; i++)
                    randWords.Add(constWords[i % constWords.Length]);
            }

            return randWords;
        }

        public List<string> GetRandDefs(int n)
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            List<string> defs = new List<string>();

            for (int i = 0; i < n; i++)
            {
                string[] defList = words[rand.Next(words.Count)].GetDefinition().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(d => d[0] != '"').ToArray();
                defs.Add(defList[rand.Next(defList.Length)]);
            }

            return defs;
        }

        List<Entry> copyList(List<Entry> orig)
        {
            List<Entry> copy = new List<Entry>();

            foreach (Entry word in orig)
                copy.Add(word);

            return copy;
        }

        void checkWotDs()
        {
            foreach (WordOfTheDay wotd in wotds)
                if (wotd.active && wotd.AnyNewPosts())
                {
                    prefs.NewWotDs = true;
                    break;
                }

            buttNewWotD.Visible = prefs.NewWotDs;
            prefs.Save();
        }


        public formMain()
        {
            InitializeComponent();
        }

        private void formMain_Load(object sender, EventArgs e)
        {
            prefs = new Preferences();

            needWotDCheck = DateTime.Now.DayOfYear != prefs.LastFeedCheck.DayOfYear;
            if (!needWotDCheck)
                buttNewWotD.Visible = prefs.NewWotDs;

            loadWords();

            //icon
            if (File.Exists(Application.StartupPath + "\\Wordy.ico"))
                this.Icon = new Icon(Application.StartupPath + "\\Wordy.ico");

            //show tutorial
            new Tutorial(Application.StartupPath + "\\tutorials\\main.txt", this);

            //check for update
            bool[] askPermissions = new bool[3] { true, true, true };
            for (int i = 0; i < prefs.UpdateNotifs; i++)
                askPermissions[i] = false;

            Updater.Update(0.95, "https://docs.google.com/document/pub?id=11hVN4KxQ1WTqQVAy2DvQZ889uBr_DvLFsn1pb0zgeP8", askPermissions, prefs.ShowChangelog);
        }

        private void formMain_Activated(object sender, EventArgs e)
        {
            if (needWotDCheck)
            {
                needWotDCheck = false;

                lblCheckingWotDs.Visible = true;
                this.Refresh();

                LoadSubs();
                checkWotDs();

                prefs.LastFeedCheck = DateTime.Now;
                prefs.Save();

                lblCheckingWotDs.Visible = false;
            }
        }

        private void buttAdd_Click(object sender, EventArgs e)
        {
            formAddWords addWords = new formAddWords();

            addWords.main = this;
            addWords.chkNewWordsFile = true;

            addWords.Show();
            this.Hide();
        }

        private void buttOptions_Click(object sender, EventArgs e)
        {
            formOptions wordlist = new formOptions();

            wordlist.main = this;
            wordlist.words = words;
            wordlist.wotds = wotds;

            wordlist.Show();
            this.Hide();
        }
        
        private void buttStudyWords_Click(object sender, EventArgs e)
        {
            if (words.Any(w => !w.archived && w.CanTest()))
            {
                formTestRecall test = new formTestRecall();

                test.main = this;
                test.newWords = true;
                test.words = copyList(words.Where(w => !w.archived && w.CanTest()).ToList());
                test.Text = "Study New Words";

                test.Show();
                this.Hide();
            }
            else if (words.Any(w => !w.archived))
                MessageBox.Show("No new words that haven't been tested recently.");
            else
                MessageBox.Show("No new words.");
        }

        private void buttRecall_Click(object sender, EventArgs e)
        {
            if (words.Any(w => w.archived && w.CanTest()))
            {
                formTestRecall test = new formTestRecall();

                test.main = this;
                test.newWords = false;
                test.words = copyList(words.Where(w => w.archived && w.CanTest()).ToList());

                test.Text = "Test Learned Words";

                test.Show();
                this.Hide();
            }
            else
                MessageBox.Show("No learned words that haven't been tested recently.");
        }

        private void buttNewWotD_Click(object sender, EventArgs e)
        {
            if (wotds == null)
                LoadSubs();

            prefs.NewWotDs = false;
            prefs.Save();

            formAddWords addWords = new formAddWords();
            addWords.main = this;

            addWords.Show();
            this.Hide();

            foreach (WordOfTheDay wotd in wotds)
                if (wotd.active && wotd.AnyNewPosts())
                    addWords.loadWotDs(wotd.getNewWordsLinks(), wotd.getNewWords());

            SaveSubs();
            buttNewWotD.Visible = false;
        }
    }
}
