using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Joi.H.AppUI
{
    /// <summary>NodeRegistry 解析出的规范节点记录。</summary>
    internal readonly struct AppUIFocusResolvedNode
    {
        public AppUIFocusResolvedNode(
            UIPageInteractionHandle pageHandle,
            string scopeId,
            string regionId,
            AppUIFocusNodeAddress nodeAddress,
            Selectable selectable,
            int registrationGeneration)
        {
            PageHandle = pageHandle;
            ScopeId = scopeId ?? string.Empty;
            RegionId = regionId ?? string.Empty;
            NodeAddress = nodeAddress;
            Selectable = selectable;
            SelectedObject = selectable != null ? selectable.gameObject : null;
            SelectableInstanceId = selectable != null ? selectable.GetInstanceID() : 0;
            GameObjectInstanceId = SelectedObject != null ? SelectedObject.GetInstanceID() : 0;
            RegistrationGeneration = registrationGeneration;
        }

        private AppUIFocusResolvedNode(
            UIPageInteractionHandle pageHandle,
            string scopeId,
            string regionId,
            AppUIFocusNodeAddress nodeAddress,
            Selectable selectable,
            GameObject selectedObject,
            int selectableInstanceId,
            int gameObjectInstanceId,
            int registrationGeneration)
        {
            PageHandle = pageHandle;
            ScopeId = scopeId;
            RegionId = regionId;
            NodeAddress = nodeAddress;
            Selectable = selectable;
            SelectedObject = selectedObject;
            SelectableInstanceId = selectableInstanceId;
            GameObjectInstanceId = gameObjectInstanceId;
            RegistrationGeneration = registrationGeneration;
        }

        public UIPageInteractionHandle PageHandle { get; }

        public string ScopeId { get; }

        public string RegionId { get; }

        public AppUIFocusNodeAddress NodeAddress { get; }

        public Selectable Selectable { get; }

        public GameObject SelectedObject { get; }

        public int SelectableInstanceId { get; }

        public int GameObjectInstanceId { get; }

        public int RegistrationGeneration { get; }

        public bool IsValid
        {
            get
            {
                return PageHandle.IsValid &&
                       !string.IsNullOrEmpty(ScopeId) &&
                       !string.IsNullOrEmpty(RegionId) &&
                       NodeAddress.IsValid &&
                       Selectable != null &&
                       SelectedObject != null &&
                       SelectableInstanceId != 0 &&
                       GameObjectInstanceId != 0 &&
                       RegistrationGeneration > 0;
            }
        }

        public AppUIFocusResolvedNode WithPageHandle(UIPageInteractionHandle pageHandle)
        {
            return new AppUIFocusResolvedNode(
                pageHandle,
                ScopeId,
                RegionId,
                NodeAddress,
                Selectable,
                SelectedObject,
                SelectableInstanceId,
                GameObjectInstanceId,
                RegistrationGeneration);
        }
    }

    internal interface IAppUIFocusNodeRegistry
    {
        bool TryResolveNode(
            UIPageInteractionHandle pageHandle,
            AppUIFocusNodeAddress nodeAddress,
            out AppUIFocusResolvedNode resolvedNode);

        bool TryResolveNode(Selectable selectable, out AppUIFocusResolvedNode resolvedNode);

        bool TryResolveNode(GameObject selectedObject, out AppUIFocusResolvedNode resolvedNode);
    }

    /// <summary>
    /// 当前 App 运行期唯一的焦点节点索引。
    /// 正向表以页面实例和 NodeAddress 定位，反向表为 Selectable / GameObject 提供 O(1) 精确解析。
    /// </summary>
    internal sealed class AppUIFocusNodeRegistry : IAppUIFocusNodeRegistry
    {
        private readonly Dictionary<long, Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode>>
            nodesByPageInstance =
                new Dictionary<long, Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode>>(16);
        private readonly Dictionary<int, AppUIFocusResolvedNode> nodesBySelectableId =
            new Dictionary<int, AppUIFocusResolvedNode>(64);
        private readonly Dictionary<int, AppUIFocusResolvedNode> nodesByGameObjectId =
            new Dictionary<int, AppUIFocusResolvedNode>(64);
        private readonly List<AppUIFocusNodeAddress> pageAddressBuffer =
            new List<AppUIFocusNodeAddress>(32);

        private int nextRegistrationGeneration;

        public bool TryResolveNode(
            UIPageInteractionHandle pageHandle,
            AppUIFocusNodeAddress nodeAddress,
            out AppUIFocusResolvedNode resolvedNode)
        {
            if (!pageHandle.IsValid ||
                !nodeAddress.IsValid ||
                !nodesByPageInstance.TryGetValue(
                    pageHandle.InstanceId,
                    out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes) ||
                !pageNodes.TryGetValue(nodeAddress, out resolvedNode))
            {
                resolvedNode = default;
                return false;
            }

            if (resolvedNode.PageHandle != pageHandle)
            {
                resolvedNode = default;
                return false;
            }

            if (!IsConsistentRecord(resolvedNode))
            {
                RemoveRecordIfGenerationMatches(resolvedNode);
                resolvedNode = default;
                return false;
            }

            return true;
        }

        public bool TryResolveNode(
            Selectable selectable,
            out AppUIFocusResolvedNode resolvedNode)
        {
            if (ReferenceEquals(selectable, null))
            {
                resolvedNode = default;
                return false;
            }

            if (!nodesBySelectableId.TryGetValue(selectable.GetInstanceID(), out resolvedNode) ||
                resolvedNode.Selectable == null ||
                !ReferenceEquals(resolvedNode.Selectable, selectable) ||
                !IsConsistentRecord(resolvedNode))
            {
                if (resolvedNode.RegistrationGeneration > 0)
                {
                    RemoveRecordIfGenerationMatches(resolvedNode);
                }

                resolvedNode = default;
                return false;
            }

            return true;
        }

        public bool TryResolveNode(
            GameObject selectedObject,
            out AppUIFocusResolvedNode resolvedNode)
        {
            if (ReferenceEquals(selectedObject, null))
            {
                resolvedNode = default;
                return false;
            }

            if (!nodesByGameObjectId.TryGetValue(selectedObject.GetInstanceID(), out resolvedNode) ||
                resolvedNode.Selectable == null ||
                !ReferenceEquals(resolvedNode.SelectedObject, selectedObject) ||
                !IsConsistentRecord(resolvedNode))
            {
                if (resolvedNode.RegistrationGeneration > 0)
                {
                    RemoveRecordIfGenerationMatches(resolvedNode);
                }

                resolvedNode = default;
                return false;
            }

            return true;
        }

        internal bool TryRegister(
            UIPageInteractionHandle pageHandle,
            string scopeId,
            string regionId,
            AppUIFocusNodeAddress nodeAddress,
            Selectable selectable,
            out AppUIFocusResolvedNode resolvedNode)
        {
            resolvedNode = default;
            if (!pageHandle.IsValid ||
                string.IsNullOrEmpty(scopeId) ||
                string.IsNullOrEmpty(regionId) ||
                !nodeAddress.IsValid ||
                selectable == null ||
                selectable.gameObject == null)
            {
                return false;
            }

            Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes =
                GetOrCreatePageNodes(pageHandle.InstanceId);
            if (pageNodes.TryGetValue(nodeAddress, out AppUIFocusResolvedNode addressOwner))
            {
                if (IsConsistentRecord(addressOwner) &&
                    ReferenceEquals(addressOwner.Selectable, selectable) &&
                    addressOwner.PageHandle == pageHandle &&
                    string.Equals(addressOwner.ScopeId, scopeId, StringComparison.Ordinal) &&
                    string.Equals(addressOwner.RegionId, regionId, StringComparison.Ordinal))
                {
                    resolvedNode = addressOwner;
                    return true;
                }

                if (IsConsistentRecord(addressOwner))
                {
                    return false;
                }

                RemoveRecordIfGenerationMatches(addressOwner);
                pageNodes = GetOrCreatePageNodes(pageHandle.InstanceId);
            }

            int selectableId = selectable.GetInstanceID();
            if (HasLiveSelectableOwner(selectableId))
            {
                return false;
            }

            GameObject selectedObject = selectable.gameObject;
            int gameObjectId = selectedObject.GetInstanceID();
            if (HasLiveGameObjectOwner(gameObjectId))
            {
                return false;
            }

            EnsureNavigationNone(selectable, scopeId, nodeAddress);
            int generation = GetNextRegistrationGeneration();
            resolvedNode = new AppUIFocusResolvedNode(
                pageHandle,
                scopeId,
                regionId,
                nodeAddress,
                selectable,
                generation);

            pageNodes.Add(nodeAddress, resolvedNode);
            nodesBySelectableId.Add(selectableId, resolvedNode);
            nodesByGameObjectId.Add(gameObjectId, resolvedNode);
            return true;
        }

        /// <summary>
        /// 在所有地址和反向归属验证通过后，一次替换一个 Group 的完整索引快照。
        /// 失败时不移除该 Group 的旧记录。
        /// </summary>
        internal bool TryReplaceGroup(
            UIPageInteractionHandle pageHandle,
            string scopeId,
            string regionId,
            string groupId,
            IReadOnlyList<AppUIFocusStagedNode> stagedNodes,
            List<AppUIFocusResolvedNode> replacementRecords)
        {
            replacementRecords?.Clear();
            if (!pageHandle.IsValid ||
                string.IsNullOrEmpty(scopeId) ||
                string.IsNullOrEmpty(regionId) ||
                string.IsNullOrEmpty(groupId) ||
                stagedNodes == null ||
                replacementRecords == null)
            {
                return false;
            }

            nodesByPageInstance.TryGetValue(
                pageHandle.InstanceId,
                out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes);

            List<AppUIFocusResolvedNode> oldRecords =
                new List<AppUIFocusResolvedNode>(16);
            Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> oldByAddress =
                new Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode>(16);
            if (pageNodes != null)
            {
                foreach (KeyValuePair<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pair in pageNodes)
                {
                    AppUIFocusResolvedNode record = pair.Value;
                    if (IsReplacementOwner(record, pageHandle.InstanceId, scopeId, groupId))
                    {
                        oldRecords.Add(record);
                        oldByAddress[pair.Key] = record;
                    }
                }
            }

            HashSet<AppUIFocusNodeAddress> stagedAddresses =
                new HashSet<AppUIFocusNodeAddress>();
            HashSet<int> stagedSelectableIds = new HashSet<int>();
            HashSet<int> stagedGameObjectIds = new HashSet<int>();
            for (int i = 0; i < stagedNodes.Count; i++)
            {
                AppUIFocusStagedNode stagedNode = stagedNodes[i];
                Selectable selectable = stagedNode.Selectable;
                if (!stagedNode.NodeKey.IsValid ||
                    selectable == null ||
                    selectable.gameObject == null)
                {
                    return false;
                }

                AppUIFocusNodeAddress address =
                    new AppUIFocusNodeAddress(groupId, stagedNode.NodeKey);
                int selectableId = selectable.GetInstanceID();
                int gameObjectId = selectable.gameObject.GetInstanceID();
                if (!address.IsValid ||
                    !stagedAddresses.Add(address) ||
                    !stagedSelectableIds.Add(selectableId) ||
                    !stagedGameObjectIds.Add(gameObjectId))
                {
                    return false;
                }

                if (pageNodes != null &&
                    pageNodes.TryGetValue(address, out AppUIFocusResolvedNode addressOwner))
                {
                    if (!IsConsistentRecord(addressOwner))
                    {
                        RemoveRecordIfGenerationMatches(addressOwner);
                    }
                    else if (!IsReplacementOwner(
                                 addressOwner,
                                 pageHandle.InstanceId,
                                 scopeId,
                                 groupId))
                    {
                        return false;
                    }
                }

                if (HasConflictingSelectableOwner(
                        selectableId,
                        pageHandle.InstanceId,
                        scopeId,
                        groupId) ||
                    HasConflictingGameObjectOwner(
                        gameObjectId,
                        pageHandle.InstanceId,
                        scopeId,
                        groupId))
                {
                    return false;
                }
            }

            int requiredGenerations = 0;
            for (int i = 0; i < stagedNodes.Count; i++)
            {
                AppUIFocusStagedNode stagedNode = stagedNodes[i];
                AppUIFocusNodeAddress address =
                    new AppUIFocusNodeAddress(groupId, stagedNode.NodeKey);
                if (!oldByAddress.TryGetValue(address, out AppUIFocusResolvedNode oldRecord) ||
                    !IsConsistentRecord(oldRecord) ||
                    !ReferenceEquals(oldRecord.Selectable, stagedNode.Selectable) ||
                    oldRecord.PageHandle != pageHandle ||
                    !string.Equals(oldRecord.RegionId, regionId, StringComparison.Ordinal))
                {
                    requiredGenerations++;
                }
            }

            if ((long)nextRegistrationGeneration + requiredGenerations > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "AppUI focus registration generation exhausted.");
            }

            for (int i = 0; i < stagedNodes.Count; i++)
            {
                AppUIFocusStagedNode stagedNode = stagedNodes[i];
                AppUIFocusNodeAddress address =
                    new AppUIFocusNodeAddress(groupId, stagedNode.NodeKey);
                EnsureNavigationNone(stagedNode.Selectable, scopeId, address);

                if (oldByAddress.TryGetValue(address, out AppUIFocusResolvedNode oldRecord) &&
                    IsConsistentRecord(oldRecord) &&
                    ReferenceEquals(oldRecord.Selectable, stagedNode.Selectable) &&
                    oldRecord.PageHandle == pageHandle &&
                    string.Equals(oldRecord.RegionId, regionId, StringComparison.Ordinal))
                {
                    replacementRecords.Add(oldRecord);
                }
                else
                {
                    replacementRecords.Add(
                        new AppUIFocusResolvedNode(
                            pageHandle,
                            scopeId,
                            regionId,
                            address,
                            stagedNode.Selectable,
                            GetNextRegistrationGeneration()));
                }
            }

            for (int i = 0; i < oldRecords.Count; i++)
            {
                RemoveRecordIfGenerationMatches(oldRecords[i]);
            }

            if (replacementRecords.Count == 0)
            {
                return true;
            }

            pageNodes = GetOrCreatePageNodes(pageHandle.InstanceId);
            for (int i = 0; i < replacementRecords.Count; i++)
            {
                AppUIFocusResolvedNode record = replacementRecords[i];
                pageNodes.Add(record.NodeAddress, record);
                nodesBySelectableId.Add(record.SelectableInstanceId, record);
                nodesByGameObjectId.Add(record.GameObjectInstanceId, record);
            }

            return true;
        }

        internal bool Unregister(
            long pageInstanceId,
            string scopeId,
            AppUIFocusNodeAddress nodeAddress,
            int expectedRegistrationGeneration)
        {
            if (pageInstanceId <= 0 ||
                string.IsNullOrEmpty(scopeId) ||
                !nodeAddress.IsValid ||
                !nodesByPageInstance.TryGetValue(
                    pageInstanceId,
                    out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes) ||
                !pageNodes.TryGetValue(nodeAddress, out AppUIFocusResolvedNode record) ||
                !string.Equals(record.ScopeId, scopeId, StringComparison.Ordinal) ||
                (expectedRegistrationGeneration > 0 &&
                 record.RegistrationGeneration != expectedRegistrationGeneration))
            {
                return false;
            }

            RemoveRecordIfGenerationMatches(record);
            return true;
        }

        internal void UpdatePageHandle(
            long pageInstanceId,
            UIPageInteractionHandle pageHandle)
        {
            if (pageInstanceId <= 0 ||
                !pageHandle.IsValid ||
                pageHandle.InstanceId != pageInstanceId ||
                !nodesByPageInstance.TryGetValue(
                    pageInstanceId,
                    out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes))
            {
                return;
            }

            pageAddressBuffer.Clear();
            foreach (KeyValuePair<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pair in pageNodes)
            {
                pageAddressBuffer.Add(pair.Key);
            }

            for (int i = 0; i < pageAddressBuffer.Count; i++)
            {
                AppUIFocusNodeAddress address = pageAddressBuffer[i];
                if (!pageNodes.TryGetValue(address, out AppUIFocusResolvedNode oldRecord))
                {
                    continue;
                }

                AppUIFocusResolvedNode newRecord = oldRecord.WithPageHandle(pageHandle);
                pageNodes[address] = newRecord;
                ReplaceReverseRecord(oldRecord, newRecord);
            }

            pageAddressBuffer.Clear();
        }

        internal void RemoveScope(long pageInstanceId, string scopeId)
        {
            if (pageInstanceId <= 0 ||
                string.IsNullOrEmpty(scopeId) ||
                !nodesByPageInstance.TryGetValue(
                    pageInstanceId,
                    out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes))
            {
                return;
            }

            foreach (KeyValuePair<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pair in pageNodes)
            {
                AppUIFocusResolvedNode record = pair.Value;
                if (string.Equals(record.ScopeId, scopeId, StringComparison.Ordinal))
                {
                    RemoveReverseRecordIfGenerationMatches(record);
                }
            }

            nodesByPageInstance.Remove(pageInstanceId);
        }

        internal void Clear()
        {
            nodesByPageInstance.Clear();
            nodesBySelectableId.Clear();
            nodesByGameObjectId.Clear();
            pageAddressBuffer.Clear();
        }

        private Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> GetOrCreatePageNodes(
            long pageInstanceId)
        {
            if (!nodesByPageInstance.TryGetValue(
                    pageInstanceId,
                    out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes))
            {
                pageNodes =
                    new Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode>(32);
                nodesByPageInstance.Add(pageInstanceId, pageNodes);
            }

            return pageNodes;
        }

        private bool HasLiveSelectableOwner(int selectableId)
        {
            if (!nodesBySelectableId.TryGetValue(
                    selectableId,
                    out AppUIFocusResolvedNode existing))
            {
                return false;
            }

            if (IsConsistentRecord(existing))
            {
                return true;
            }

            RemoveRecordIfGenerationMatches(existing);
            return false;
        }

        private bool HasLiveGameObjectOwner(int gameObjectId)
        {
            if (!nodesByGameObjectId.TryGetValue(
                    gameObjectId,
                    out AppUIFocusResolvedNode existing))
            {
                return false;
            }

            if (IsConsistentRecord(existing))
            {
                return true;
            }

            RemoveRecordIfGenerationMatches(existing);
            return false;
        }

        private bool HasConflictingSelectableOwner(
            int selectableId,
            long pageInstanceId,
            string scopeId,
            string groupId)
        {
            if (!nodesBySelectableId.TryGetValue(
                    selectableId,
                    out AppUIFocusResolvedNode existing))
            {
                return false;
            }

            if (!IsConsistentRecord(existing))
            {
                RemoveRecordIfGenerationMatches(existing);
                return false;
            }

            return !IsReplacementOwner(existing, pageInstanceId, scopeId, groupId);
        }

        private bool HasConflictingGameObjectOwner(
            int gameObjectId,
            long pageInstanceId,
            string scopeId,
            string groupId)
        {
            if (!nodesByGameObjectId.TryGetValue(
                    gameObjectId,
                    out AppUIFocusResolvedNode existing))
            {
                return false;
            }

            if (!IsConsistentRecord(existing))
            {
                RemoveRecordIfGenerationMatches(existing);
                return false;
            }

            return !IsReplacementOwner(existing, pageInstanceId, scopeId, groupId);
        }

        private static bool IsReplacementOwner(
            AppUIFocusResolvedNode record,
            long pageInstanceId,
            string scopeId,
            string groupId)
        {
            return record.PageHandle.InstanceId == pageInstanceId &&
                   string.Equals(record.ScopeId, scopeId, StringComparison.Ordinal) &&
                   string.Equals(
                       record.NodeAddress.GroupId,
                       groupId,
                       StringComparison.Ordinal);
        }

        private bool IsConsistentRecord(AppUIFocusResolvedNode record)
        {
            if (!record.IsValid ||
                !ReferenceEquals(record.Selectable.gameObject, record.SelectedObject) ||
                record.Selectable.GetInstanceID() != record.SelectableInstanceId ||
                record.SelectedObject.GetInstanceID() != record.GameObjectInstanceId ||
                !nodesByPageInstance.TryGetValue(
                    record.PageHandle.InstanceId,
                    out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes) ||
                !pageNodes.TryGetValue(
                    record.NodeAddress,
                    out AppUIFocusResolvedNode forwardRecord) ||
                forwardRecord.RegistrationGeneration != record.RegistrationGeneration ||
                !ReferenceEquals(forwardRecord.Selectable, record.Selectable))
            {
                return false;
            }

            return nodesBySelectableId.TryGetValue(
                       record.SelectableInstanceId,
                       out AppUIFocusResolvedNode selectableRecord) &&
                   selectableRecord.RegistrationGeneration == record.RegistrationGeneration &&
                   nodesByGameObjectId.TryGetValue(
                       record.GameObjectInstanceId,
                       out AppUIFocusResolvedNode gameObjectRecord) &&
                   gameObjectRecord.RegistrationGeneration == record.RegistrationGeneration;
        }

        private void RemoveRecordIfGenerationMatches(AppUIFocusResolvedNode record)
        {
            if (record.RegistrationGeneration <= 0)
            {
                return;
            }

            if (nodesByPageInstance.TryGetValue(
                    record.PageHandle.InstanceId,
                    out Dictionary<AppUIFocusNodeAddress, AppUIFocusResolvedNode> pageNodes) &&
                pageNodes.TryGetValue(
                    record.NodeAddress,
                    out AppUIFocusResolvedNode current) &&
                current.RegistrationGeneration == record.RegistrationGeneration)
            {
                pageNodes.Remove(record.NodeAddress);
                if (pageNodes.Count == 0)
                {
                    nodesByPageInstance.Remove(record.PageHandle.InstanceId);
                }
            }

            RemoveReverseRecordIfGenerationMatches(record);
        }

        private void RemoveReverseRecordIfGenerationMatches(AppUIFocusResolvedNode record)
        {
            if (record.SelectableInstanceId != 0 &&
                nodesBySelectableId.TryGetValue(
                    record.SelectableInstanceId,
                    out AppUIFocusResolvedNode selectableRecord) &&
                selectableRecord.RegistrationGeneration == record.RegistrationGeneration)
            {
                nodesBySelectableId.Remove(record.SelectableInstanceId);
            }

            if (record.GameObjectInstanceId != 0 &&
                nodesByGameObjectId.TryGetValue(
                    record.GameObjectInstanceId,
                    out AppUIFocusResolvedNode gameObjectRecord) &&
                gameObjectRecord.RegistrationGeneration == record.RegistrationGeneration)
            {
                nodesByGameObjectId.Remove(record.GameObjectInstanceId);
            }
        }

        private void ReplaceReverseRecord(
            AppUIFocusResolvedNode oldRecord,
            AppUIFocusResolvedNode newRecord)
        {
            if (!IsConsistentRecord(oldRecord))
            {
                RemoveRecordIfGenerationMatches(oldRecord);
                return;
            }

            if (nodesBySelectableId.TryGetValue(
                    oldRecord.SelectableInstanceId,
                    out AppUIFocusResolvedNode selectableRecord) &&
                selectableRecord.RegistrationGeneration == oldRecord.RegistrationGeneration)
            {
                nodesBySelectableId[oldRecord.SelectableInstanceId] = newRecord;
            }

            if (nodesByGameObjectId.TryGetValue(
                    oldRecord.GameObjectInstanceId,
                    out AppUIFocusResolvedNode gameObjectRecord) &&
                gameObjectRecord.RegistrationGeneration == oldRecord.RegistrationGeneration)
            {
                nodesByGameObjectId[oldRecord.GameObjectInstanceId] = newRecord;
            }
        }

        private int GetNextRegistrationGeneration()
        {
            if (nextRegistrationGeneration == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "AppUI focus registration generation exhausted.");
            }

            nextRegistrationGeneration++;
            return nextRegistrationGeneration;
        }

        private static void EnsureNavigationNone(
            Selectable selectable,
            string scopeId,
            AppUIFocusNodeAddress nodeAddress)
        {
            Navigation navigation = selectable.navigation;
            if (navigation.mode == Navigation.Mode.None)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "<AppUIFocus> Registered Selectable must use Navigation.Mode.None. Scope=" +
                scopeId +
                ", Node=" +
                nodeAddress +
                ", Object=" +
                selectable.name);
#endif
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }
    }
}
