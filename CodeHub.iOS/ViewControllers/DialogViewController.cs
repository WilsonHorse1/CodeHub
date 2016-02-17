//
// DialogViewController.cs: drives MonoTouch.Dialog
//
// Author:
//   Miguel de Icaza
//
// Code to support pull-to-refresh based on Martin Bowling's TweetTableView
// which is based in turn in EGOTableViewPullRefresh code which was created
// by Devin Doty and is Copyrighted 2009 enormego and released under the
// MIT X11 license
//
using System;
using UIKit;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using CodeHub.iOS.DialogElements;
using System.Linq;

namespace CodeHub.iOS.ViewControllers
{
	/// <summary>
	///   The DialogViewController is the main entry point to use MonoTouch.Dialog,
	///   it provides a simplified API to the UITableViewController.
	/// </summary>
    public class DialogViewController : TableViewController
	{
        private readonly Lazy<RootElement> _rootElement;
		public UISearchBar searchBar;
		bool pushing;

		/// <summary>
		/// The root element displayed by the DialogViewController, the value can be changed during runtime to update the contents.
		/// </summary>
		public RootElement Root {
			get 
            {
                return _rootElement.Value;
			}
		} 

		// If the value is true, we are enabled, used in the source for quick computation
		bool enableSearch;
		public bool EnableSearch {
			get {
				return enableSearch;
			}
			set {
				if (enableSearch == value)
					return;
				enableSearch = value;
			}
		}

		// If set, we automatically scroll the content to avoid showing the search bar until 
		// the user manually pulls it down.
		public bool AutoHideSearch { get; set; }

		public string SearchPlaceholder { get; set; }

		public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
		{
			base.DidRotate (fromInterfaceOrientation);
			ReloadData ();
		}

		Section [] originalSections;
		Element [][] originalElements;

		/// <summary>
		/// Allows caller to programatically activate the search bar and start the search process
		/// </summary>
		public void StartSearch ()
		{
			if (originalSections != null)
				return;

			searchBar.BecomeFirstResponder ();
            CreateOriginals(Root);
		}

        private void CreateOriginals(RootElement root)
        {
            originalSections = root.Sections.ToArray ();
            originalElements = new Element [originalSections.Length][];
            for (int i = 0; i < originalSections.Length; i++)
                originalElements [i] = originalSections [i].Elements.ToArray ();
        }

		/// <summary>
		/// Allows the caller to programatically stop searching.
		/// </summary>
		public virtual void FinishSearch ()
		{
			if (originalSections == null)
				return;

            searchBar.Text = "";

            Root.Reset(originalSections);
			originalSections = null;
			originalElements = null;
			searchBar.ResignFirstResponder ();
			ReloadData ();
		}

		public void PerformFilter (string text)
		{
			if (originalSections == null)
				return;

			var newSections = new List<Section> ();

			for (int sidx = 0; sidx < originalSections.Length; sidx++){
				Section newSection = null;
				var section = originalSections [sidx];
				Element [] elements = originalElements [sidx];

				for (int eidx = 0; eidx < elements.Length; eidx++){
					if (elements [eidx].Matches (text)){
						if (newSection == null){
							newSection = new Section (section.Header, section.Footer){
								FooterView = section.FooterView,
								HeaderView = section.HeaderView
							};
							newSections.Add (newSection);
						}
						newSection.Add (elements [eidx]);
					}
				}
			}

            Root.Reset(newSections);
			ReloadData ();
		}

		public virtual void SearchButtonClicked (string text)
		{
			searchBar.ResignFirstResponder();
		}

		protected class SearchDelegate : UISearchBarDelegate {
			readonly WeakReference<DialogViewController> container;

			public SearchDelegate (DialogViewController container)
			{
                this.container = new WeakReference<DialogViewController>(container);
			}

			public override void OnEditingStarted (UISearchBar searchBar)
			{
				searchBar.ShowsCancelButton = true;
                container.Get()?.StartSearch ();
			}

			public override void OnEditingStopped (UISearchBar searchBar)
			{
				searchBar.ShowsCancelButton = false;
				//container.FinishSearch ();
			}

			public override void TextChanged (UISearchBar searchBar, string searchText)
			{
                container.Get()?.PerformFilter (searchText ?? "");
			}

			public override void CancelButtonClicked (UISearchBar searchBar)
			{
                var r = container.Get();
				searchBar.ShowsCancelButton = false;
                if (r != null)
                {
                    r.searchBar.Text = "";
                    r.FinishSearch();
                }
				searchBar.ResignFirstResponder ();
			}

			public override void SearchButtonClicked (UISearchBar searchBar)
			{
                container.Get()?.SearchButtonClicked (searchBar.Text);
			}
		}

        protected virtual void DidScroll(CGPoint p)
        {
        }

        public class Source : UITableViewSource {
            private readonly WeakReference<DialogViewController> _container;
            private readonly RootElement _root;

            public DialogViewController Container
            {
                get { return _container.Get(); }
            }

			public Source (DialogViewController container)
			{
                _container = new WeakReference<DialogViewController>(container);
                _root = container.Root;
			}

			public override nint RowsInSection (UITableView tableview, nint section)
			{
                var s = _root[(int)section];
				var count = s?.Elements.Count;
				return count ?? 0;
			}

			public override nint NumberOfSections (UITableView tableView)
			{
                return _root.Count;
			}

			public override string TitleForHeader (UITableView tableView, nint section)
			{
                return _root[(int)section]?.Header;
			}

			public override string TitleForFooter (UITableView tableView, nint section)
			{
                return _root[(int)section]?.Footer;
			}

			public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
			{
                var section = _root[indexPath.Section];
				var element = section?[indexPath.Row];
				return element?.GetCell (tableView);
			}

			public override void RowDeselected (UITableView tableView, NSIndexPath indexPath)
			{
                _container.Get()?.Deselected (indexPath);
			}

			public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
			{
                _container.Get()?.Selected (indexPath);
			}			

			public override UIView GetViewForHeader (UITableView tableView, nint sectionIdx)
			{
                var section = _root[(int)sectionIdx];
				return section?.HeaderView;
			}

			public override nfloat GetHeightForHeader (UITableView tableView, nint sectionIdx)
			{
                var section = _root[(int)sectionIdx];
				return section?.HeaderView?.Frame.Height ?? -1;
			}

			public override UIView GetViewForFooter (UITableView tableView, nint sectionIdx)
			{
                var section = _root[(int)sectionIdx];
				return section.FooterView;
			}

			public override nfloat GetHeightForFooter (UITableView tableView, nint sectionIdx)
			{
                var section = _root[(int)sectionIdx];
                return section?.FooterView?.Frame.Height ?? -1;
			}

			public override void Scrolled (UIScrollView scrollView)
			{
                _container.Get()?.DidScroll(_root.TableView.ContentOffset);
			}

            public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
            {
                var section = _root[indexPath.Section];
                var element = section?[indexPath.Row];

                var sizable = element as IElementSizing;
                return sizable?.GetHeight(tableView, indexPath) ?? tableView.RowHeight;
            }
		}

        protected virtual IUISearchBarDelegate CreateSearchDelegate()
        {
            return new SearchDelegate(this);
        }

		void SetupSearch ()
		{
			if (enableSearch){
                searchBar = new UISearchBar (new CGRect (0, 0, TableView.Bounds.Width, 44)) {
                    Delegate = CreateSearchDelegate()
				};
				if (SearchPlaceholder != null)
					searchBar.Placeholder = this.SearchPlaceholder;
                TableView.TableHeaderView = searchBar;					
			} else {
				// Does not work with current Monotouch, will work with 3.0
				// tableView.TableHeaderView = null;
			}
		}

		public virtual void Deselected (NSIndexPath indexPath)
		{
			var section = Root[indexPath.Section];
			var element = section[indexPath.Row];

			element.Deselected (TableView, indexPath);
		}

		public virtual void Selected (NSIndexPath indexPath)
		{
            var section = Root[indexPath.Section];
			var element = section[indexPath.Row];

            element.Selected (TableView, indexPath);
		}

        public virtual Source CreateSizingSource()
        {
            return new Source (this);
        }


		public override void LoadView ()
		{
            base.LoadView();
			SetupSearch ();
            TableView.Source = CreateSizingSource();
		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			if (AutoHideSearch){
				if (enableSearch){
					if (TableView.ContentOffset.Y < 44)
						TableView.ContentOffset = new CGPoint (0, 44);
				}
			}

			NavigationItem.HidesBackButton = !pushing;
			TableView.ReloadData ();
		}

		public bool Pushing {
			get {
				return pushing;
			}
			set {
				pushing = value;
				if (NavigationItem != null)
					NavigationItem.HidesBackButton = !pushing;
			}
		}

		public void ReloadData ()
		{
			TableView.ReloadData ();
		}

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            TableView.CellLayoutMarginsFollowReadableWidth = false;
        }

		public DialogViewController (UITableViewStyle style, bool pushing = true) 
            : base (style)
		{
            _rootElement = new Lazy<RootElement>(() => new RootElement(TableView));

            EdgesForExtendedLayout = UIRectEdge.None;
            SearchPlaceholder = "Search";
            NavigationItem.BackBarButtonItem = new UIBarButtonItem { Title = "" };
			this.pushing = pushing;
		}
	}
}