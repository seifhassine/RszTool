﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using RszTool.App.Common;

namespace RszTool.App.ViewModels
{
    public abstract class BaseRszFileViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public abstract BaseRszFile File { get; }
        public string? FilePath => File.FileHandler.FilePath;
        public string? FileName
        {
            get
            {
                string? path = FilePath;
                return path != null ? Path.GetFileName(path) : null;
            }
        }
        public bool Changed { get; protected set; }
        public RelayCommand CopyArrayItem => new(OnCopyArrayItem);
        public RelayCommand RemoveArrayItem => new(OnRemoveArrayItem);
        public RelayCommand DuplicateArrayItem => new(OnDuplicateArrayItem);

        public static RszInstance? CopiedInstance { get; private set; }

        public bool Save() => File.Save();

        public bool SaveAs(string path)
        {
            bool result = File.SaveAs(path);
            if (result)
            {
                PropertyChanged?.Invoke(this, new(nameof(FilePath)));
            }
            return result;
        }

        public virtual void PostRead() {}

        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected static void OnCopyArrayItem(object arg)
        {
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                CopiedInstance = item.Instance;
            }
        }

        protected void OnRemoveArrayItem(object arg)
        {
            if (arg is RszFieldArrayInstanceItemViewModel item && File.GetRSZ() is RSZFile rsz)
            {
                rsz.ArrayRemoveItem(item.Values, item.Instance);
            }
        }

        protected void OnDuplicateArrayItem(object arg)
        {
            if (arg is RszFieldArrayInstanceItemViewModel item && File.GetRSZ() is RSZFile rsz)
            {
                rsz.ArrayInsertItem(item.Values, item.Instance, isDuplicate: true);
            }
        }
    }


    public class UserFileViewModel(UserFile file) : BaseRszFileViewModel
    {
        public override UserFile File { get; } = file;

        public RszViewModel RszViewModel => new(File.RSZ!);
    }


    public class PfbFileViewModel(PfbFile file) : BaseRszFileViewModel
    {
        public override PfbFile File { get; } = file;
        public RszViewModel RszViewModel => new(File.RSZ!);
        public IEnumerable<PfbFile.GameObjectData>? GameObjects => File.GameObjectDatas;

        public override void PostRead()
        {
            File.SetupGameObjects();
        }
    }


    public class ScnFileViewModel(ScnFile file) : BaseRszFileViewModel
    {
        public override ScnFile File { get; } = file;
        public RszViewModel RszViewModel => new(File.RSZ!);
        public ObservableCollection<ScnFile.FolderData>? Folders => File.FolderDatas;
        public ObservableCollection<ScnFile.GameObjectData>? GameObjects => File.GameObjectDatas;

        public static ScnFile.GameObjectData? CopiedGameObject { get; private set; }

        public override void PostRead()
        {
            File.SetupGameObjects();
        }

        public RelayCommand CopyGameObject => new(OnCopyGameObject);
        public RelayCommand RemoveGameObject => new(OnRemoveGameObject);
        public RelayCommand DuplicateGameObject => new(OnDuplicateGameObject);
        public RelayCommand PasetGameObject => new(OnPasetGameObject);
        public RelayCommand PasetGameObjectToFolder => new(OnPasetGameObjectToFolder);
        public RelayCommand PasetGameObjectToParent => new(OnPasetGameObjectToParent);

        /// <summary>
        /// 复制游戏对象
        /// </summary>
        /// <param name="arg"></param>
        public static void OnCopyGameObject(object arg)
        {
            CopiedGameObject = (ScnFile.GameObjectData)arg;
        }

        /// <summary>
        /// 删除游戏对象
        /// </summary>
        /// <param name="arg"></param>
        public void OnRemoveGameObject(object arg)
        {
            File.RemoveGameObject((ScnFile.GameObjectData)arg);
        }

        /// <summary>
        /// 重复游戏对象
        /// </summary>
        /// <param name="arg"></param>
        public void OnDuplicateGameObject(object arg)
        {
            File.DuplicateGameObject((ScnFile.GameObjectData)arg);
        }

        /// <summary>
        /// 粘贴游戏对象
        /// </summary>
        /// <param name="arg"></param>
        public void OnPasetGameObject(object arg)
        {
            if (CopiedGameObject != null)
            {
                File.ImportGameObject(CopiedGameObject);
                OnPropertyChanged(nameof(GameObjects));
            }
        }

        /// <summary>
        /// 粘贴游戏对象到文件夹
        /// </summary>
        /// <param name="arg"></param>
        public void OnPasetGameObjectToFolder(object arg)
        {
            if (CopiedGameObject != null)
            {
                var folder = (ScnFile.FolderData)arg;
                File.ImportGameObject(CopiedGameObject, folder);
            }
        }

        /// <summary>
        /// 粘贴游戏对象到父对象
        /// </summary>
        /// <param name="arg"></param>
        public void OnPasetGameObjectToParent(object arg)
        {
            if (CopiedGameObject != null)
            {
                var parent = (ScnFile.GameObjectData)arg;
                File.ImportGameObject(CopiedGameObject, parent: parent);
            }
        }
    }


    public class RszViewModel(RSZFile rsz)
    {
        public List<RszInstance> Instances => rsz.InstanceList;

        public IEnumerable<RszInstance> Objects => rsz.ObjectList;
    }
}
