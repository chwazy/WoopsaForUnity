﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Woopsa
{
    /// <summary>
    /// Use this attribute to decorate the methods and properties of normal objects and qualify it hey must be published by woopsa
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class WoopsaVisibleAttribute : Attribute
    {
        public WoopsaVisibleAttribute(bool visible = true)
        {
            Visible = visible;
        }

        public bool Visible { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class WoopsaValueTypeAttribute : Attribute
    {
        public WoopsaValueTypeAttribute(WoopsaValueType valueType)
        {
            ValueType = valueType;
        }

        public WoopsaValueType ValueType { get; private set; }
    }

    [Flags]
    public enum WoopsaVisibility
    {
        /// <summary>
        /// Publish normal members decorated with WoopsaVisible attribute and declared within the class
        /// </summary>
        None = 0,
        /// <summary>
        /// For members not decorated with WoopsaVisibleAttribute, consider the default value of WoopsaVisible as true
        /// </summary>
        DefaultIsVisible = 1,
        /// <summary>
        /// Publish methods with special names (like property getters, setters).
        /// </summary>
        MethodSpecialName = 2,
        /// <summary>
        /// Publish inherited members.
        /// </summary>
        Inherited = 4,
        /// <summary>
        /// Publish IEnumerable<Object> compatible types as a collection of items.
        /// </summary>
        IEnumerableObject = 8,
        /// <summary>
        /// Publish members inherited from Object, like ToString. Requires flag Inherited to have an effect.
        /// </summary>
        ObjectClassMembers = 16,
        /// <summary>
        /// Publish members inherited from ArrayList, List<>, Collection, ObservableCollection
        /// </summary>
        ListClassMembers = 32,
        /// <summary>
        /// Publish all
        /// </summary>
        All = DefaultIsVisible | MethodSpecialName | Inherited | IEnumerableObject | ObjectClassMembers | ListClassMembers
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class WoopsaVisibilityAttribute : Attribute
    {
        public WoopsaVisibilityAttribute(WoopsaVisibility visibility)
        {
            Visibility = visibility;
        }

        public WoopsaVisibility Visibility { get; private set; }
    }

    [Flags]
    public enum WoopsaObjectAdapterOptions
    {
        None = 0,
        SendTimestamps = 2
    }

    public class EventArgsMemberVisibilityCheck : EventArgs
    {
        public EventArgsMemberVisibilityCheck(MemberInfo member)
        {
            Member = member;
        }

        public MemberInfo Member { get; private set; }

        public bool IsVisible { get; set; }
    }

    public class WoopsaObjectAdapter : WoopsaObject
    {
        public const WoopsaVisibility DefaultDefaultVisibility =
            WoopsaVisibility.DefaultIsVisible | WoopsaVisibility.Inherited;

        #region static

        public const string EnumerableItemDefaultBaseName = "Item";

        public static string EnumerableItemDefaultName(long id)
        {
            return EnumerableItemDefaultBaseName + id.ToString();
        }

        public static int EnumerableItemIdFromDefaultName(string itemName)
        {
            return int.Parse(itemName.Substring(EnumerableItemDefaultBaseName.Length));
        }

        #endregion static

        /// <summary>
        /// Create a WoopsaObjectAdapter for a fixed object reference
        /// </summary>
        /// <param name="container">Can be null if it's the root object.</param>
        /// <param name="name"></param>
        /// <param name="targetObjectGetter"></param>
        /// <param name="declaredExposedType">
        /// Type to expose for the targetObject. 
        /// Specify null to use targetObject.GetType().
        /// The specified type can be different than the targetObject.GetType() 
        /// when an interface is published instead of the public methods of the type.
        /// Specifying null implies that the type is determined dynamically, as targetObjectGetter
        /// does not necesarily return always the same object.
        /// </param>
        /// <param name="options"></param>
        /// <param name="defaultVisibility"></param>
        public WoopsaObjectAdapter(WoopsaContainer container, string name, object targetObject,
                Type declaredExposedType = null,
                WoopsaConverters customValueTypeConverters = null,
                WoopsaObjectAdapterOptions options = WoopsaObjectAdapterOptions.None,
                WoopsaVisibility defaultVisibility = DefaultDefaultVisibility) :
            this(container, name, () => targetObject, declaredExposedType,
                new TypeDescriptions(customValueTypeConverters),
                options, defaultVisibility)
        {
        }

        /// <summary>
        /// Create a WoopsaObjectAdapter for a dynamic object reference, returned by a delegate
        /// </summary>
        /// <param name="container"></param>
        /// <param name="name"></param>
        /// <param name="targetObjectGetter"></param>
        /// <param name="declaredExposedType">
        /// Type to expose for the targetObject. 
        /// Specify null to use targetObject.GetType().
        /// The specified type can be different than the targetObject.GetType() 
        /// when an interface is published instead of the public methods of the type.
        /// Specifying null implies that the type is determined dynamically, as targetObjectGetter
        /// does not necesarily return always the same object.
        /// </param>
        /// <param name="options"></param>
        /// <param name="defaultVisibility"></param>
        protected WoopsaObjectAdapter(WoopsaContainer container, string name, Func<object> targetObjectGetter,
            Type declaredExposedType,
            TypeDescriptions typeDescriptions,
            WoopsaObjectAdapterOptions options = WoopsaObjectAdapterOptions.None,
            WoopsaVisibility defaultVisibility = DefaultDefaultVisibility)
            : base(container, name)
        {
            TargetObjectGetter = targetObjectGetter;
            DeclaredExposedType = declaredExposedType;
            DefaultVisibility = defaultVisibility;
            Options = options;
            TypeDescriptions = typeDescriptions;
            _lock = new object();
        }

        /// <summary>
        /// To customize the woopsa visibility of a member. 
        /// This event is triggered for every member, including the members of the inner items.
        /// It can be used to force the visibility of any member to true or false.
        /// </summary>
        public event EventHandler<EventArgsMemberVisibilityCheck> MemberWoopsaVisibilityCheck;

        public object TargetObject
        {
            get
            {
                object newTargetObject, previousTargetObject;
                try
                {
                    newTargetObject = TargetObjectGetter();
                }
                catch (Exception)
                {
                    newTargetObject = null;
                    // Items from property getters that throw exceptions are not added to the object hierarchy
                    // Ignore silently
                }
                if (newTargetObject != _targetObject)
                {
                    lock (_lock)
                    {
                        UnsubscribeCollectionChanged();                        
                        object currentTargetObjectType = _targetObject != null ? ExposedType(_targetObject) : null;
                        object newTargetObjectType = newTargetObject != null ? ExposedType(newTargetObject) : null;
                        if (newTargetObjectType != currentTargetObjectType)
                        {
                            Clear();
                            DoPopulate();
                        }
                        else
                        {
                            Type exposedType;
                            exposedType = GetExposedType(newTargetObject);
                            previousTargetObject = _targetObject;
                            _targetObject = newTargetObject;
                            OnTargetObjectChange(previousTargetObject, newTargetObject, exposedType);
                        }
                    }
                }
                return _targetObject;
            }
        }
        public Func<object> TargetObjectGetter { get; private set; }
        public WoopsaObjectAdapterOptions Options { get; private set; }
        public TypeDescriptions TypeDescriptions { get; private set; }

        /// <summary>
        /// Visibility for the WoopsaObjectAdapter and its inner WoopsaObjectAdapters.
        /// Applies if the TargetObject is not decorated with the WoopsaVisilibityAttribute
        /// </summary>
        public WoopsaVisibility DefaultVisibility { get; private set; }

        /// <summary>
        /// Visibility for this WoopsaObjectAdapter
        /// </summary>
        public WoopsaVisibility Visibility { get; private set; }

        public string OrderedItemIds
        {
            get
            {
                UpdateItems();
                bool first = true;
                // Format as a Json array
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("[");
                if (_enumerableItems != null)
                {
                    var sortedItemAdapters = _enumerableItems.Values.OrderBy(s => s.EnumerableItemIndex);
                    foreach (var item in sortedItemAdapters)
                    {
                        if (!first)
                            stringBuilder.Append(",");
                        stringBuilder.Append(item.EnumerableItemId.ToString());
                        first = false;
                    }
                }
                stringBuilder.Append("]");
                return stringBuilder.ToString();
            }
        }

        #region Private/Protected Methods

        protected override void Clear()
        {
            base.Clear();
            if (_enumerableItems != null)
                _enumerableItems = null;
            UnsubscribeCollectionChanged();
        }

        protected virtual void OnMemberWoopsaVisibilityCheck(EventArgsMemberVisibilityCheck e)
        {
            if (MemberWoopsaVisibilityCheck != null)
                MemberWoopsaVisibilityCheck(this, e);
            else if (Owner is WoopsaObjectAdapter)
                ((WoopsaObjectAdapter)Owner).OnMemberWoopsaVisibilityCheck(e);
        }

        protected override void PopulateObject()
        {
            TypeDescription typeDescription = null;
            object newTargetObject, previousTargetObject;
            try
            {
                newTargetObject = TargetObjectGetter();
            }
            catch (Exception)
            {
                newTargetObject = null;
                // Items from property getters that throw exceptions are not added to the object hierarchy
                // Ignore silently
            }
            if (newTargetObject != null)
            {
                WoopsaVisibilityAttribute woopsaVisibilityAttribute =
                    WoopsaReflection.GetCustomAttribute<WoopsaVisibilityAttribute>(newTargetObject.GetType());
                if (woopsaVisibilityAttribute != null)
                    Visibility = woopsaVisibilityAttribute.Visibility;
                else
                    Visibility = DefaultVisibility;
            }
            else
                Visibility = DefaultVisibility;
            Type exposedType;
            exposedType = GetExposedType(newTargetObject);
            previousTargetObject = _targetObject;
            _targetObject = newTargetObject;
            OnTargetObjectChange(previousTargetObject, newTargetObject, exposedType);
            base.PopulateObject();
            if (newTargetObject != null)
            {
                typeDescription = GetTypeDescription(exposedType);
                PopulateProperties(newTargetObject, exposedType, typeDescription.Properties);
                PopulateMethods(newTargetObject, exposedType, typeDescription.Methods);
                PopulateItems(newTargetObject, exposedType, typeDescription.Items);
                if (typeof(IEnumerable<object>).IsAssignableFrom(exposedType) && Visibility.HasFlag(WoopsaVisibility.IEnumerableObject))
                {
                    IEnumerable enumerable = (IEnumerable)newTargetObject;
                    PopulateEnumerableItems(enumerable, DeclaredExposedType);
                }
            }
        }

        private Type GetExposedType(object targetObject)
        {
            Type exposedType;
            if (targetObject != null)
                exposedType = ExposedType(targetObject);
            else
                exposedType = null;
            return exposedType;
        }

        protected virtual void OnTargetObjectChange(object previousTargetObject, object newTargetObject, Type exposedType)
        {
        }

        protected TypeDescription GetTypeDescription(Type exposedType)
        {
            TypeDescription typeDescription;
            lock (TypeDescriptions)
                typeDescription = TypeDescriptions.GetTypeDescription(exposedType);
            return typeDescription;
        }

        protected virtual Type ExposedType(object targetObject)
        {
            if (DeclaredExposedType != null)
                return DeclaredExposedType;
            else
                return targetObject.GetType();
        }

        protected virtual Type ItemExposedType(Type itemDeclaredType)
        {
            if (DeclaredExposedType != null)
                // Use the declared type for items as well 
                return itemDeclaredType;
            else
                // Use effective type for items as well, so don't specify an exposedType
                return null;
        }

        protected override void UpdateItems()
        {
            base.UpdateItems();
            // Enforce object update if needed (implemented in TargetObjectGetter) :
            IEnumerable enumerable = TargetObject as IEnumerable;
            if (enumerable != null)
                // If we cannot know if the enumerable has changed, or if we know it has changed
                // then we update
                if (_iNotifyCollectionChanged == null || _collectionChanged)
                {
                    if (_enumerableItems != null)
                    {
                        HashSet<object> existingItems = new HashSet<object>();
                        // Add missing items
                        foreach (var item in enumerable)
                        {
                            if (item != null)
                            {
                                if (!_enumerableItems.ContainsKey(item))
                                    AddEnumerableItem(item);
                                existingItems.Add(item);
                            }
                        }
                        // Remove items that have disappeared
                        foreach (var item in _enumerableItems.Keys.ToArray())
                            if (!existingItems.Contains(item))
                                DeleteEnumerableItem(item);
                        // Order items
                        int index = 0;
                        foreach (var item in enumerable)
                            if (item != null)
                            {
                                WoopsaObjectAdapter itemAdapter;
                                if (_enumerableItems.TryGetValue(item, out itemAdapter))
                                    itemAdapter.EnumerableItemIndex = index;
                                index++;
                            }
                    }
                }
        }

        protected virtual void PopulateProperties(object targetObject, Type exposedType,
            IEnumerable<PropertyDescription> properties)
        {
            HashSet<string> addedElements = new HashSet<string>();

            foreach (var property in properties)
                if (IsMemberWoopsaVisible(targetObject, property.PropertyInfo))
                    if (!addedElements.Contains(property.Name))
                    {
                        AddWoopsaProperty(property);
                        addedElements.Add(property.Name);
                    }
            if (typeof(IEnumerable<object>).IsAssignableFrom(exposedType) && Visibility.HasFlag(WoopsaVisibility.IEnumerableObject))
                new WoopsaProperty(this, nameof(OrderedItemIds), WoopsaValueType.JsonData,
                   (p) => WoopsaValue.WoopsaJsonData(OrderedItemIds));
        }

        protected virtual void PopulateMethods(object targetObject, Type exposedType,
            IEnumerable<MethodDescription> methods)
        {
            HashSet<string> addedElements = new HashSet<string>();

            foreach (var method in methods)
                if (IsMemberWoopsaVisible(targetObject, method.MethodInfo))
                    if (!addedElements.Contains(method.Name))
                    {
                        AddWoopsaMethod(method);
                        addedElements.Add(method.Name);
                    }
        }

        protected virtual void PopulateItems(object targetObject, Type exposedType, IEnumerable<ItemDescription> items)
        {
            HashSet<string> addedElements = new HashSet<string>();

            foreach (var item in items)
            {
                if (IsMemberWoopsaVisible(targetObject, item.PropertyInfo))
                    if (!addedElements.Contains(item.Name))
                    {
                        CreateItemWoopsaAdapter(
                            item.Name,
                            () => item.PropertyInfo.GetValue(TargetObject, EmptyParameters),
                            ItemExposedType(item.PropertyInfo.PropertyType));
                        addedElements.Add(item.Name);
                    }
            }
        }

        protected virtual void PopulateEnumerableItems(IEnumerable enumerable, Type exposedType)
        {
            if (_enumerableItems == null)
                _enumerableItems = new Dictionary<object, WoopsaObjectAdapter>();
            if (exposedType != null)
                WoopsaTypeUtils.GetGenericEnumerableItemType(exposedType, out _itemExposedType);
            _iNotifyCollectionChanged = enumerable as INotifyCollectionChanged;
            if (_iNotifyCollectionChanged != null)
                _iNotifyCollectionChanged.CollectionChanged += EnumerableCollectionChanged;
            int index = 0;
            foreach (object item in enumerable)
                if (item != null)
                {
                    WoopsaObjectAdapter adapter = AddEnumerableItem(item);
                    adapter.EnumerableItemIndex = index;
                    index++;
                }
        }

        protected virtual bool IsMemberWoopsaVisible(object targetObject, MemberInfo memberInfo)
        {
            var woopsaVisibleAttribute = WoopsaReflection.GetCustomAttribute<WoopsaVisibleAttribute>(memberInfo);
            bool isVisible;
            Type targetType = targetObject.GetType();
            if (woopsaVisibleAttribute != null)
                isVisible = woopsaVisibleAttribute.Visible;
            else
                isVisible = Visibility.HasFlag(WoopsaVisibility.DefaultIsVisible);
            if (isVisible)
            {
                if (memberInfo.DeclaringType != targetType)
                    isVisible = Visibility.HasFlag(WoopsaVisibility.Inherited);
            }
            if (isVisible)
            {
                if (memberInfo.DeclaringType == typeof(object))
                    isVisible = Visibility.HasFlag(WoopsaVisibility.ObjectClassMembers);
            }
            if (isVisible)
            {
                if (memberInfo.DeclaringType == typeof(ArrayList) ||
                    (memberInfo.DeclaringType.IsGenericType &&
                        (memberInfo.DeclaringType.GetGenericTypeDefinition() == typeof(List<>) ||
                        memberInfo.DeclaringType.GetGenericTypeDefinition() == typeof(Collection<>) ||
                        memberInfo.DeclaringType.GetGenericTypeDefinition() == typeof(ObservableCollection<>)))
                   )
                    isVisible = Visibility.HasFlag(WoopsaVisibility.ListClassMembers);
            }
            if (isVisible)
            {
                if (memberInfo is MethodBase)
                    if ((memberInfo as MethodBase).IsSpecialName)
                        isVisible = Visibility.HasFlag(WoopsaVisibility.MethodSpecialName);
            }
            if (isVisible)
            {
                if (memberInfo is PropertyInfo)
                {
                    PropertyInfo property = (PropertyInfo)memberInfo;
                    if (typeof(IEnumerable<object>).IsAssignableFrom(property.PropertyType))
                        isVisible = Visibility.HasFlag(WoopsaVisibility.IEnumerableObject);
                }
            }
            EventArgsMemberVisibilityCheck e = new EventArgsMemberVisibilityCheck(memberInfo);
            e.IsVisible = isVisible;
            OnMemberWoopsaVisibilityCheck(e);
            isVisible = e.IsVisible;
            return isVisible;
        }

        protected DateTime? GetTimeStamp()
        {
            if (Options.HasFlag(WoopsaObjectAdapterOptions.SendTimestamps))
                return DateTime.Now;
            else
                return null;
        }

        protected void AddWoopsaProperty(PropertyDescription propertyDescription)
        {
            WoopsaValueType publishedWoopsaPropertyType = PublishedWoopsaPropertyType(propertyDescription);
            if (propertyDescription.IsReadOnly)
                new WoopsaProperty(this, propertyDescription.PropertyInfo.Name, publishedWoopsaPropertyType,
                    CreateWoopsaPropertyGetDelegate(publishedWoopsaPropertyType, propertyDescription)
                );
            else
                new WoopsaProperty(this, propertyDescription.PropertyInfo.Name, publishedWoopsaPropertyType,
                    CreateWoopsaPropertyGetDelegate(publishedWoopsaPropertyType, propertyDescription),
                    CreateWoopsaPropertySetDelegate(publishedWoopsaPropertyType, propertyDescription)
                );
        }

        protected virtual WoopsaPropertyGet CreateWoopsaPropertyGetDelegate(
            WoopsaValueType publishedWoopsaPropertyType, PropertyDescription propertyDescription)
        {
            return (sender) => (propertyDescription.Converter.ToWoopsaValue(
                propertyDescription.PropertyInfo.GetValue(TargetObject, EmptyParameters),
                publishedWoopsaPropertyType, GetTimeStamp()));
        }

        protected virtual WoopsaPropertySet CreateWoopsaPropertySetDelegate(
            WoopsaValueType publishedWoopsaPropertyType, PropertyDescription propertyDescription)
        {
            return (sender, value) => propertyDescription.PropertyInfo.SetValue(
                TargetObject,
                propertyDescription.Converter.FromWoopsaValue(value, propertyDescription.PropertyInfo.PropertyType),
                null);
        }

        protected virtual WoopsaValueType PublishedWoopsaPropertyType(PropertyDescription property)
        {
            return property.WoopsaType;
        }

        protected virtual WoopsaObjectAdapter CreateItemWoopsaAdapter(string name,
            Func<object> itemGetter, Type exposedType)
        {
            return new WoopsaObjectAdapter(this, name, itemGetter, exposedType, TypeDescriptions, Options, Visibility);
        }

        protected void AddWoopsaMethod(MethodDescription methodDescription)
        {
            try
            {
                new WoopsaMethod(this, methodDescription.MethodInfo.Name, methodDescription.WoopsaReturnType,
                    methodDescription.WoopsaArguments, CreateWoopsaMethodInvokeDelegate(methodDescription));
            }
            catch (Exception)
            {
                // This can happen when methods are overloaded. This isn't supported in Woopsa.
                // Ignore silently, the method won't be published
            }
        }

        protected virtual WoopsaMethodInvoke CreateWoopsaMethodInvokeDelegate(
            MethodDescription methodDescription)
        {
            return (args) =>
            {
                var typedArguments = new object[methodDescription.Arguments.Count];
                for (var i = 0; i < methodDescription.Arguments.Count; i++)
                    typedArguments[i] = methodDescription.Arguments[i].Converter.FromWoopsaValue(
                        (IWoopsaValue)args[i], methodDescription.Arguments[i].Type);
                if (methodDescription.MethodInfo.ReturnType == typeof(void))
                {
                    methodDescription.MethodInfo.Invoke(TargetObject, typedArguments);
                    return null;
                }
                else
                    return methodDescription.Converter.ToWoopsaValue(
                        methodDescription.MethodInfo.Invoke(TargetObject, typedArguments), methodDescription.WoopsaReturnType,
                        GetTimeStamp());
            };
        }

        protected Type DeclaredExposedType { get; private set; }

        protected long EnumerableItemId { get; private set; }

        protected int EnumerableItemIndex { get; private set; }

        protected virtual string EnumerableItemName(object enumerableItem, long enumerableItemId)
        {
            return EnumerableItemDefaultName(enumerableItemId);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            UnsubscribeCollectionChanged();
        }

        #endregion

        private void EnumerableCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _collectionChanged = true;
        }

        private void UnsubscribeCollectionChanged()
        {
            if (_iNotifyCollectionChanged != null)
            {
                _iNotifyCollectionChanged.CollectionChanged -= EnumerableCollectionChanged;
                _iNotifyCollectionChanged = null;
                _collectionChanged = false;
            }
        }

        private WoopsaObjectAdapter AddEnumerableItem(object item)
        {
            WoopsaObjectAdapter itemAdapter = CreateItemWoopsaAdapter(EnumerableItemName(item, _nextEnumerableItemId),
                () => item, _itemExposedType);
            itemAdapter.EnumerableItemId = _nextEnumerableItemId;
            _enumerableItems[item] = itemAdapter;
            _nextEnumerableItemId++;
            return itemAdapter;
        }

        private void DeleteEnumerableItem(object item)
        {
            WoopsaObjectAdapter enumerableItemAdapter;
            if (_enumerableItems.TryGetValue(item, out enumerableItemAdapter))
            {
                enumerableItemAdapter.Dispose();
                _enumerableItems.Remove(item);
            }
        }

        private object _targetObject;
        private object _lock;
        private Dictionary<object, WoopsaObjectAdapter> _enumerableItems;
        private long _nextEnumerableItemId;
        private Type _itemExposedType;
        private INotifyCollectionChanged _iNotifyCollectionChanged;
        private bool _collectionChanged;

        protected static readonly object[] EmptyParameters = new object[] { };
    }

}