using System;
using System.Runtime.CompilerServices;
using Flecs.NET.Core;
using Flecs.NET.Core.Hooks;
using Xunit;
using static Flecs.NET.Bindings.flecs;

namespace Flecs.NET.Tests.CSharp.Core
{
    public unsafe class ReflectionTests
    {
        [Fact]
        private void ManualCharMemberKeepsFollowingOffset()
        {
            using World world = World.Create();

            Entity type = world.Component<CharStruct>()
                .Member<char>("C")
                .Member<int>("I");

            EcsStruct meta = type.Get<EcsStruct>();
            ecs_member_t* members = (ecs_member_t*)meta.members.array;
            Assert.Equal(2, meta.members.count);
            Assert.Equal(0, members[0].offset);
            Assert.Equal(4, members[1].offset);

            // Two chars in a row: flecs' 1-byte EcsChar would put B at offset 1 and size the struct 2.
            Entity pair = world.Component<CharPair>()
                .Member<char>("A")
                .Member<char>("B");

            meta = pair.Get<EcsStruct>();
            members = (ecs_member_t*)meta.members.array;
            Assert.Equal(0, members[0].offset);
            Assert.Equal(2, members[1].offset);
            Assert.Equal(4, pair.Get<EcsComponent>().size);

            CharPair pairValue = new CharPair { A = 'a', B = 'Ж' };
            Assert.Equal("{\"A\":97, \"B\":1046}", world.ToJson(ref pairValue));
        }

        [Fact]
        private void LoneSurrogateRoundTrips()
        {
            using World world = World.Create();

            world.Component<CharStruct>("CharStruct")
                .Member<char>("C")
                .Member<int>("I");

            CharStruct value = new CharStruct { C = '\uD800', I = 1 };
            Assert.Equal("{\"C\":55296, \"I\":1}", world.ToJson(ref value));

            CharStruct parsed = default;
            Assert.True(world.FromJson(Type<CharStruct>.Id(world), &parsed, """
                {"C": 55296, "I": 1}
                """));
            Assert.Equal('\uD800', parsed.C);
        }

        [Fact]
        private void CursorRoundTripsChar()
        {
            using World world = World.Create();

            Entity type = world.Component<CharStruct>()
                .Member<char>("C")
                .Member<int>("I");

            CharStruct value = default;
            Cursor cursor = world.Cursor(type, &value);
            cursor.Push();
            Assert.Equal(0, cursor.SetChar('Ж'));
            cursor.Next();
            Assert.Equal(0, cursor.SetInt(3));
            cursor.Pop();

            Assert.Equal('Ж', value.C);
            Assert.Equal(3, value.I);

            cursor = world.Cursor(type, &value);
            cursor.Push();
            Assert.Equal('Ж', cursor.GetChar());
            cursor.Next();
            Assert.Equal(3, cursor.GetInt());
            cursor.Pop();
        }

        [Fact]
        private void CursorRoundTripsNativeChar()
        {
            using World world = World.Create();

            Entity type = world.Component("NativeChars")
                .Member(Ecs.Char, "C")
                .Member(Ecs.I32, "I");

            NativeCharStruct value = default;
            Cursor cursor = world.Cursor(type.Id, &value);
            cursor.Push();
            Assert.Equal(0, cursor.SetChar('A'));
            cursor.Next();
            Assert.Equal(0, cursor.SetInt(3));
            cursor.Pop();

            Assert.Equal((byte)'A', value.C);
            Assert.Equal(3, value.I);

            cursor = world.Cursor(type.Id, &value);
            cursor.Push();
            Assert.Equal('A', cursor.GetChar());
            cursor.Pop();
        }

        [Fact]
        private void ScriptVarCharAboveAscii()
        {
            using World world = World.Create();

            byte native = 200;
            world.ConstVar("native_char", Ecs.Char, &native);
            Assert.Equal((char)200, world.GetConstVar<char>("native_char"));

            world.MutVar("mutable_char", Ecs.Char, &native);
            Assert.Equal((char)200, world.GetMutVar<char>("mutable_char"));
        }

        [Fact]
        private void FixedBufferReflectsArrayAndFollowingMember()
        {
            using World world = World.Create();

            Entity type = world.Component<FixedBufferStruct>();
            Assert.True(type.Has<EcsStruct>());

            EcsStruct meta = type.Get<EcsStruct>();
            ecs_member_t* members = (ecs_member_t*)meta.members.array;
            Assert.Equal(2, meta.members.count);
            Assert.Equal(4, members[0].count);
            Assert.Equal(0, members[0].offset);
            Assert.Equal(16, members[1].offset);

            FixedBufferStruct value = default;
            value.Values[0] = 1;
            value.Values[3] = 4;
            value.Tail = 7;

            Entity entity = world.Entity("e2").Set(value);
            Assert.Contains("[1, 0, 0, 4]", entity.ToJson());
            Assert.Equal("{Values: [1, 0, 0, 4], Tail: 7}", world.ToExpr(ref value));

            Entity parsed = world.Entity("e3").SetJson<FixedBufferStruct>("""
                {"Values": [5, 6, 7, 8], "Tail": 9}
                """);

            FixedBufferStruct parsedValue = parsed.Get<FixedBufferStruct>();
            Assert.Equal(5, parsedValue.Values[0]);
            Assert.Equal(8, parsedValue.Values[3]);
            Assert.Equal(9, parsedValue.Tail);
        }

        [Fact]
        private void PointerFieldReflectsAsUPtrAndKeepsFollowingOffsets()
        {
            using World world = World.Create();

            Entity type = world.Component<PointerStruct>();
            Assert.True(type.Has<EcsStruct>());

            // flecs maps a C pointer member to uptr, so the following member stays at offset 8.
            EcsStruct meta = type.Get<EcsStruct>();
            ecs_member_t* members = (ecs_member_t*)meta.members.array;
            Assert.Equal(2, meta.members.count);
            Assert.Equal(0, members[0].offset);
            Assert.Equal(8, members[1].offset);

            int value = 42;
            PointerStruct data = new PointerStruct { Pointer = &value, Tail = 7 };
            Entity entity = world.Entity("e5").Set(data);

            Assert.Equal(7, entity.Get<PointerStruct>().Tail);
            Assert.Equal(42, *entity.Get<PointerStruct>().Pointer);
        }

        [Fact]
        private void AutoPropertyKeepsDeclarationOrder()
        {
            using World world = World.Create();

            Entity type = world.Component<MixedFields>();
            Assert.True(type.Has<EcsStruct>());

            EcsStruct meta = type.Get<EcsStruct>();
            ecs_member_t* members = (ecs_member_t*)meta.members.array;
            Assert.Equal(3, meta.members.count);
            Assert.Equal(0, members[0].offset);
            Assert.Equal(4, members[1].offset);
            Assert.Equal(8, members[2].offset);
            Assert.Equal(12, type.Get<EcsComponent>().size);

            MixedFields value = new MixedFields { A = 1, B = 2, C = 3 };
            Assert.Equal("{\"A\":1, \"B\":2, \"C\":3}", world.ToJson(ref value));

            Entity parsed = world.Entity("e1").SetJson<MixedFields>("""
                {"A": 4, "B": 5, "C": 6}
                """);

            MixedFields parsedValue = parsed.Get<MixedFields>();
            Assert.Equal(4, parsedValue.A);
            Assert.Equal(5, parsedValue.B);
            Assert.Equal(6, parsedValue.C);
        }

        [Fact]
        private void PrivateFieldKeepsDeclarationOrder()
        {
            using World world = World.Create();

            Entity type = world.Component<PrivateFieldBetween>();
            Assert.True(type.Has<EcsStruct>());

            EcsStruct meta = type.Get<EcsStruct>();
            ecs_member_t* members = (ecs_member_t*)meta.members.array;
            Assert.Equal(3, meta.members.count);
            Assert.Equal(0, members[0].offset);
            Assert.Equal(4, members[1].offset);
            Assert.Equal(8, members[2].offset);

            Entity parsed = world.Entity("e1").SetJson<PrivateFieldBetween>("""
                {"A": 4, "B": 5, "C": 6}
                """);

            PrivateFieldBetween parsedValue = parsed.Get<PrivateFieldBetween>();
            Assert.Equal(4, parsedValue.A);
            Assert.Equal(5, parsedValue.BValue);
            Assert.Equal(6, parsedValue.C);
        }

        [Fact]
        private void RecordStructReflectsPositionalMembers()
        {
            using World world = World.Create();

            Entity type = world.Component<RecordPosition>();
            Assert.True(type.Has<EcsStruct>());

            EcsStruct meta = type.Get<EcsStruct>();
            ecs_member_t* members = (ecs_member_t*)meta.members.array;
            Assert.Equal(2, meta.members.count);
            Assert.Equal(0, members[0].offset);
            Assert.Equal(4, members[1].offset);

            RecordPosition value = new RecordPosition(1, 2);
            Assert.Equal("{\"X\":1, \"Y\":2}", world.ToJson(ref value));

            Entity parsed = world.Entity("e1").SetJson<RecordPosition>("""
                {"X": 3, "Y": 4}
                """);

            Assert.Equal(new RecordPosition(3, 4), parsed.Get<RecordPosition>());
        }

        [Fact]
        private void SetAndGet()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(CreatePlayer("hello", 42, 1, 2, 3));

            FixedPlayer player = entity.Get<FixedPlayer>();

            Assert.Equal("hello", player.Name.ToString());
            Assert.Equal(42, player.Score);
            Assert.Equal(3, player.Items.Count);
            Assert.Equal(1, player.Items[0]);
            Assert.Equal(3, player.Items[2]);
        }

        [Fact]
        private void CloneCopiesValues()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(CreatePlayer("hello", 42, 1, 2, 3));
            Entity clone = entity.Clone();

            FixedPlayer cloned = clone.Get<FixedPlayer>();
            cloned.Name.TryCopyFrom("changed");
            cloned.Items.RemoveAt(0);
            cloned.Score = 7;
            clone.Set(cloned);

            FixedPlayer original = entity.Get<FixedPlayer>();

            Assert.Equal("hello", original.Name.ToString());
            Assert.Equal(42, original.Score);
            Assert.Equal(3, original.Items.Count);
            Assert.Equal(1, original.Items[0]);

            FixedPlayer updated = clone.Get<FixedPlayer>();
            Assert.Equal("changed", updated.Name.ToString());
            Assert.Equal(7, updated.Score);
            Assert.Equal(2, updated.Items.Count);
        }

        [Fact]
        private void PrefabInstantiateCopiesValues()
        {
            using World world = World.Create();

            Entity prefab = world.Prefab("p1").Set(CreatePlayer("prefab", 42, 1, 2, 3));
            Entity instance = world.Entity("e1").IsA(prefab);

            FixedPlayer player = instance.Get<FixedPlayer>();

            Assert.Equal("prefab", player.Name.ToString());
            Assert.Equal(42, player.Score);
            Assert.Equal(3, player.Items.Count);
        }

        [Fact]
        private void TableMovePreservesValues()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(CreatePlayer("hello", 42, 1, 2, 3));
            entity.Add<Tag>();

            FixedPlayer player = entity.Get<FixedPlayer>();

            Assert.Equal("hello", player.Name.ToString());
            Assert.Equal(42, player.Score);
            Assert.Equal(3, player.Items.Count);
        }

        [Fact]
        private void DeleteDoesNotCrash()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(CreatePlayer("hello", 42, 1, 2, 3));
            entity.Destruct();

            Assert.False(entity.IsAlive());
        }

        [Fact]
        private void JsonRoundTrip()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(CreatePlayer("hello", 42, 1, 2, 3));
            string json = entity.ToJson();

            Assert.Contains("hello", json);
            Assert.Contains("[1, 2, 3]", json);
            Assert.Contains("42", json);

            Entity deserialized = world.Entity("e2").SetJson<FixedPlayer>("""
                {"Name": "world", "Items": [7, 8], "Score": 1}
                """);

            FixedPlayer player = deserialized.Get<FixedPlayer>();

            Assert.Equal("world", player.Name.ToString());
            Assert.Equal(1, player.Score);
            Assert.Equal(2, player.Items.Count);
            Assert.Equal(7, player.Items[0]);
            Assert.Equal(8, player.Items[1]);
        }

        [Fact]
        private void JsonTruncatesOverlongStringAndClampsArray()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").SetJson<FixedPlayer>("""
                {"Name": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Items": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]}
                """);

            FixedPlayer player = entity.Get<FixedPlayer>();

            // FlecsString64 stores at most 62 bytes, the array keeps the first elements and the
            // last parsed value in the final slot.
            Assert.Equal(FlecsString64.Capacity, player.Name.Length);
            Assert.Equal(FlecsArray8<int>.Capacity, player.Items.Count);
            Assert.Equal(1, player.Items[0]);
            Assert.Equal(8, player.Items[FlecsArray8<int>.Capacity - 1]);
        }

        [Fact]
        private void ScriptAssignsValues()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(default(FixedPlayer));

            world.ScriptRun("fixed_types", """
                e1 { FixedPlayer: { Name: "script", Items: [1, 2, 3], Score: 5 } }
                """);

            FixedPlayer player = entity.Get<FixedPlayer>();

            Assert.Equal("script", player.Name.ToString());
            Assert.Equal(5, player.Score);
            Assert.Equal(3, player.Items.Count);
            Assert.Equal(1, player.Items[0]);
            Assert.Equal(3, player.Items[2]);
        }

        [Fact]
        private void ScriptTruncatesOverlongString()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(default(FixedPlayer));

            world.ScriptRun("fixed_types_truncate", """
                e1 { FixedPlayer: { Name: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" } }
                """);

            FixedPlayer player = entity.Get<FixedPlayer>();

            // FlecsString64 stores at most 62 bytes.
            Assert.Equal(FlecsString64.Capacity, player.Name.Length);
            Assert.Equal(new string('a', FlecsString64.Capacity), player.Name.ToString());
        }

        [Fact]
        private void ScriptClampsOverCapacityArray()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(default(FixedPlayer));

            world.ScriptRun("fixed_types_clamp", """
                e1 { FixedPlayer: { Items: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] } }
                """);

            FixedPlayer player = entity.Get<FixedPlayer>();

            // The array keeps the first elements and the last parsed value in the final slot.
            Assert.Equal(FlecsArray8<int>.Capacity, player.Items.Count);
            Assert.Equal(1, player.Items[0]);
            Assert.Equal(8, player.Items[FlecsArray8<int>.Capacity - 1]);
        }

        [Fact]
        private void JsonSerializesNestedArrays()
        {
            using World world = World.Create();

            Entity entity = world.Entity("e1").Set(new FixedStrings { Names = ["a", "b"] });
            string json = entity.ToJson();

            Assert.Contains("[\"a\", \"b\"]", json);
        }

        [Fact]
        private void DirectComponents()
        {
            using World world = World.Create();

            Entity name = world.Entity("e1").Set(new FlecsString64("hello"));
            FlecsArray8<int> array = [1, 2, 3];
            Entity items = world.Entity("e2").Set(array);

            Assert.Contains("\"hello\"", name.ToJson());
            Assert.Contains("[1, 2, 3]", items.ToJson());

            Assert.Equal("hello", name.Get<FlecsString64>().ToString());
            Assert.Equal(3, items.Get<FlecsArray8<int>>().Count);

            Assert.True(world.FromJson(Type<FlecsString64>.Id(world), name.GetMutPtr<FlecsString64>(), "\"world\""));
            Assert.Equal("world", name.Get<FlecsString64>().ToString());

            Assert.True(world.FromJson(Type<FlecsArray8<int>>.Id(world), items.GetMutPtr<FlecsArray8<int>>(), "[7, 8]"));
            Assert.Equal(2, items.Get<FlecsArray8<int>>().Count);
            Assert.Equal(7, items.Get<FlecsArray8<int>>()[0]);
        }

        [Fact]
        private void FlagsEnums()
        {
            using World world = World.Create();

            Entity access = world.Component<Access>();
            Assert.True(access.Has<EcsBitmask>());
            Assert.False(access.Has<EcsEnum>());

            // Zero constants are not registered for bitmasks, because flecs matches constants by value.
            Assert.Equal(0ul, access.Lookup("None").Id.Value);
            Assert.Equal(0ul, world.Entity(Access.None).Id.Value);
            Assert.Throws<Ecs.AssertionException>(() => world.Entity().Add(Access.None));

            AccessHolder value = new AccessHolder { Flags = Access.Read | Access.Write };
            string json = world.ToJson(ref value);
            Assert.Contains("Read", json);
            Assert.Contains("Write", json);
            Assert.DoesNotContain("None", json);

            AccessHolder zero = default;
            Assert.Equal("{\"Flags\":0}", world.ToJson(ref zero));

            AccessHolder parsed = default;
            Assert.True(world.FromJson(Type<AccessHolder>.Id(world), &parsed, """{"Flags":"Read|Write"}"""));
            Assert.Equal(Access.Read | Access.Write, parsed.Flags);

            // Only u32 sized flags enums become bitmasks.
            Entity wide = world.Component<WideAccess>();
            Assert.True(wide.Has<EcsEnum>());
            Assert.False(wide.Has<EcsBitmask>());

            // A constant with bits that no single-bit constant covers keeps the enum path.
            Entity all = world.Component<AllAccess>();
            Assert.True(all.Has<EcsEnum>());
            Assert.False(all.Has<EcsBitmask>());

            AllAccessHolder allValue = new AllAccessHolder { Flags = AllAccess.All };
            Assert.Equal("{\"Flags\":\"All\"}", world.ToJson(ref allValue));
        }

        [Fact]
        private void InlineArrayReflectsAsFlecsArray()
        {
            using World world = World.Create();

            Entity type = world.Component<FloatValues>();
            Assert.False(type.Has<EcsStruct>());
            Assert.True(type.Has<EcsArray>());

            EcsArray array = type.Get<EcsArray>();
            Assert.Equal(Type<float>.Id(world), array.type);
            Assert.Equal(4, array.count);
            Assert.Equal(16, type.Get<EcsComponent>().size);

            FloatValues value = default;
            value[0] = 1;
            value[1] = 2;
            value[2] = 3;
            value[3] = 4;
            Assert.Equal("[1, 2, 3, 4]", world.ToJson(ref value));

            FloatValues parsed = default;
            Assert.True(world.FromJson(Type<FloatValues>.Id(world), &parsed, "[5, 6, 7, 8]"));
            Assert.Equal(5, parsed[0]);
            Assert.Equal(8, parsed[3]);
        }

        [Fact]
        private void InlineArrayMemberReflectsAsArray()
        {
            using World world = World.Create();

            Assert.True(world.Component<InlineArrayHolder>().Has<EcsStruct>());

            InlineArrayHolder value = default;
            value.Flat[0] = 1;
            value.Flat[1] = 2;
            value.Flat[2] = 3;
            value.Flat[3] = 4;
            value.Tail = 5;
            Assert.Equal("{Flat: [1, 2, 3, 4], Tail: 5}", world.ToExpr(ref value));

            InlineArrayHolder parsed = default;
            Assert.True(world.FromJson(Type<InlineArrayHolder>.Id(world), &parsed,
                """{"Flat":[5, 6, 7, 8], "Tail":9}"""));
            Assert.Equal(5, parsed.Flat[0]);
            Assert.Equal(8, parsed.Flat[3]);
            Assert.Equal(9, parsed.Tail);
        }

        [Fact]
        private void InlineArrayOfPointersReflectsAsUPtrArray()
        {
            using World world = World.Create();

            // A pointer can't be a generic type argument, so the element is reflected as uptr.
            Entity type = world.Component<PointerValues>();
            EcsArray array = type.Get<EcsArray>();
            Assert.Equal(Type<nuint>.Id(world), array.type);
            Assert.Equal(2, array.count);
            Assert.Equal(16, type.Get<EcsComponent>().size);
        }

        [Fact]
        private void ReflectedStructMemberHooksFire()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            world.Component<ReflectedOuter>();
            AssertForwardsMemberHooks(world, new ReflectedOuter { Member = new HookedMember { Value = 1 } });

            // Ordering check: register the outer type before the member type.
            using World outerFirstWorld = World.Create();
            outerFirstWorld.Component<ReflectedOuter>();
            AssertForwardsMemberHooks(outerFirstWorld, new ReflectedOuter { Member = new HookedMember { Value = 2 } });
        }

        [Fact]
        private void ManualStructMemberHooksFire()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            world.Component<ManualOuter>().Member<HookedMember>("Member");

            AssertForwardsMemberHooks(world, new ManualOuter { Member = new HookedMember { Value = 1 } });
        }

        [Fact]
        private void ManualNestedStructMemberHooksFire()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            world.Component<ManualOuter>().Member<HookedMember>("Member");
            world.Component<ManualNestedOuter>().Member<ManualOuter>("Outer");

            AssertForwardsMemberHooks(world, new ManualNestedOuter
            {
                Outer = new ManualOuter { Member = new HookedMember { Value = 1 } }
            });
        }

        [Fact]
        private void ReflectedNestedStructMemberHooksFire()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            AssertForwardsMemberHooks(world, new ReflectedNestedOuter
            {
                Outer = new ReflectedOuter { Member = new HookedMember { Value = 1 } }
            });

            // Ordering check: register the inner reflected type before the outer one.
            using World innerFirstWorld = World.Create();
            innerFirstWorld.Component<ReflectedOuter>();
            innerFirstWorld.Component<ReflectedNestedOuter>();
            AssertForwardsMemberHooks(innerFirstWorld, new ReflectedNestedOuter
            {
                Outer = new ReflectedOuter { Member = new HookedMember { Value = 2 } }
            });
        }

        [Fact]
        private void PlainStructMemberHooksDoNotFire()
        {
            // Only [FlecsStruct] types get meta reflection, so flecs has no member types to
            // forward hooks to for a plain struct. Documented current behavior: no member hooks.
            using World world = World.Create();
            world.Component<HookedMember>();
            world.Component<PlainOuter>();
            Reset();

            world.Entity().Set(new PlainOuter { Member = new HookedMember { Value = 1 } });
            Assert.Equal(0, HookedMember.CopyCount);

            Entity prefab = world.Prefab().Set(new PlainOuter { Member = new HookedMember { Value = 2 } });
            Reset();
            Entity instance = world.Entity().IsA(prefab);
            instance.Destruct();

            Assert.Equal(0, HookedMember.CopyCount);
            Assert.Equal(0, HookedMember.DtorCount);
        }

        [Fact]
        private void CallbackHookBeforeMemberKeepsUserHook()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            int userDtorCount = 0;

            world.Component<CallbackOuter>()
                .Dtor((ref CallbackOuter _, TypeInfo _) => userDtorCount++)
                .Member<HookedMember>("Member");

            Reset();
            world.Entity().Set(new CallbackOuter { Member = new HookedMember { Value = 1 } }).Destruct();

            Assert.True(userDtorCount > 0, "rtt replaced the user dtor hook");
            Assert.Equal(0, HookedMember.DtorCount);
        }

        [Fact]
        private void ObserverOnlyHookProviderStillForwards()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            ObserverOnlyOuter.OnAddCount = 0;
            world.Component<ObserverOnlyOuter>().Member<HookedMember>("Member");

            AssertForwardsMemberHooks(world, new ObserverOnlyOuter { Member = new HookedMember { Value = 1 } });
            Assert.True(ObserverOnlyOuter.OnAddCount > 0, "observer-only hook was not preserved");
        }

        [Fact]
        private void UntypedMemberForwardsHooks()
        {
            using World world = World.Create();
            world.Component<HookedMember>();

            // Existing component registered from a C# type.
            ulong id = world.Component<ManualOuter>();
            world.Component(id).Member<HookedMember>("Member");
            Reset();
            world.Entity().Set(new ManualOuter { Member = new HookedMember { Value = 1 } });
            Assert.True(HookedMember.CopyCount > 0, "untyped Member path did not forward member hooks");

            // Fresh untyped component, flecs generates the hooks itself.
            UntypedComponent fresh = world.Component("UntypedFresh").Member<HookedMember>("Member");
            ecs_type_hooks_t* freshHooks = ecs_get_hooks_id(world, fresh.Id);
            Assert.True(freshHooks != null && freshHooks->copy != null, "fresh untyped Member path has no member hooks");
        }

        [Fact]
        private void MemberAfterInUseDoesNotThrow()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            ulong id = world.Component<ManualOuter>();

            // Use the component first, e.g. to add reflection data afterwards.
            world.Entity().Set(new ManualOuter { Member = new HookedMember { Value = 1 } });
            world.Component(id).Member<HookedMember>("Member");

            ecs_type_hooks_t* hooks = ecs_get_hooks_id(world, id);
            Assert.True(hooks != null && hooks->copy == null, "in-use component had its hooks reconfigured");
        }

        [Fact]
        private void MultipleHookedMembersForward()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            world.Component<TwoHookedOuter>().Member<HookedMember>("First").Member<HookedMember>("Second");

            Reset();
            world.Entity().Set(new TwoHookedOuter
            {
                First = new HookedMember { Value = 1 },
                Second = new HookedMember { Value = 2 }
            });

            Assert.True(HookedMember.CopyCount >= 2, $"only {HookedMember.CopyCount} of 2 hooked members were forwarded");
        }

        [Fact]
        private void InlineArrayOfHookedMembersForwardsHooks()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            world.Component<HookedArray>();

            AssertForwardsMemberHooks(world, new HookedArray());
        }

        [Fact]
        private void CustomCompareBeforeMemberIsPreserved()
        {
            using World world = World.Create();
            world.Component<HookedMember>();
            int userCompareCount = 0;
            int userEqualsCount = 0;

            ulong id = world.Component<CompareOuter>()
                .OnEquals((ref CompareOuter a, ref CompareOuter b, TypeInfo _) =>
                {
                    userEqualsCount++;
                    return a.Member.Value == b.Member.Value;
                })
                .OnCompare((ref CompareOuter a, ref CompareOuter b, TypeInfo _) =>
                {
                    userCompareCount++;
                    return a.Member.Value - b.Member.Value;
                })
                .Member<HookedMember>("Member");

            CompareOuter a = new() { Member = new HookedMember { Value = 1 } };
            CompareOuter b = new() { Member = new HookedMember { Value = 2 } };

            ecs_type_info_t* ti = ecs_get_type_info(world, id);
            Assert.True((ti->hooks.flags & ECS_TYPE_HOOK_CMP_ILLEGAL) == 0,
                $"custom compare hook was dropped (flags={ti->hooks.flags})");
            Assert.True((ti->hooks.flags & ECS_TYPE_HOOK_EQUALS_ILLEGAL) == 0,
                $"custom equals hook was dropped (flags={ti->hooks.flags})");
            Assert.True(ti->hooks.cmp(&a, &b, ti) < 0, "custom compare hook was replaced by rtt");
            Assert.False(ti->hooks.equals(&a, &b, ti), "custom equals hook was replaced by rtt");
            Assert.True(userCompareCount > 0, "custom compare hook was not called");
            Assert.True(userEqualsCount > 0, "custom equals hook was not called");
        }

        private static void Reset()
        {
            HookedMember.CtorCount = 0;
            HookedMember.DtorCount = 0;
            HookedMember.CopyCount = 0;
            HookedMember.MoveCount = 0;
        }

        // Set, prefab instantiate and destruct an outer component and check that the hooked member's
        // copy and dtor hooks ran, i.e. that the outer's hooks forward to its member types.
        private static void AssertForwardsMemberHooks<TOuter>(World world, TOuter value)
        {
            Reset();
            world.Entity().Set(value);
            int copyOnSet = HookedMember.CopyCount;

            Entity prefab = world.Prefab().Set(value);
            Reset();
            Entity instance = world.Entity().IsA(prefab);
            int copyOnInstantiate = HookedMember.CopyCount;

            Reset();
            instance.Destruct();
            int dtorOnDestruct = HookedMember.DtorCount;

            string counts =
                $"copyOnSet={copyOnSet}, copyOnInstantiate={copyOnInstantiate}, dtorOnDestruct={dtorOnDestruct}";
            Assert.True(copyOnSet > 0, $"member copy hook did not fire on set ({counts})");
            Assert.True(copyOnInstantiate > 0, $"member copy hook did not fire on prefab instantiation ({counts})");
            Assert.True(dtorOnDestruct > 0, $"member dtor hook did not fire on entity destruction ({counts})");
        }

        private static FixedPlayer CreatePlayer(string name, int score, params int[] items)
        {
            FixedPlayer player = default;
            player.Name.TryCopyFrom(name);
            player.Score = score;

            foreach (int item in items)
                player.Items.Add(item);

            return player;
        }
    }
}

internal struct CharStruct
{
    public char C;
    public int I;
}

internal struct CharPair
{
    public char A;
    public char B;
}

internal struct NativeCharStruct
{
    public byte C;
    public int I;
}

[FlecsStruct]
internal unsafe struct FixedBufferStruct
{
    public fixed int Values[4];
    public int Tail;
}

[FlecsStruct]
internal unsafe struct PointerStruct
{
    public int* Pointer;
    public int Tail;
}

[FlecsStruct]
internal struct MixedFields
{
    public int A;
    public int B { get; set; }
    public int C;
}

[FlecsStruct]
internal struct PrivateFieldBetween
{
    public int A;
    private int B;
    public int C;

    public PrivateFieldBetween(int a, int b, int c)
    {
        A = a;
        B = b;
        C = c;
    }

    public readonly int BValue => B;
}

[FlecsStruct]
internal record struct RecordPosition(int X, int Y);

#pragma warning disable FLECSREFL001 // Nested in a generic type, so no registrar can name it.
internal class GenericContainer<T>
{
    [FlecsStruct]
    internal struct Nested
    {
        public int Value { get; set; }
    }
}
#pragma warning restore FLECSREFL001

#pragma warning disable FLECSREFL005 // Private nesting, so no registrar can name it.
internal class PrivateContainer
{
    [FlecsStruct]
    private struct Hidden
    {
        public int Value { get; set; }
    }
}
#pragma warning restore FLECSREFL005

[FlecsStruct]
internal struct FixedPlayer
{
    public FlecsString64 Name;
    public FlecsArray8<int> Items;
    public int Score;
}

[FlecsStruct]
internal struct FixedStrings
{
    public FlecsArray4<FlecsString32> Names;
}

[Flags]
internal enum Access
{
    None = 0,
    Read = 1,
    Write = 2
}

[Flags]
internal enum WideAccess : long
{
    None = 0,
    Read = 1
}

[FlecsStruct]
internal struct AccessHolder
{
    public Access Flags;
}

[Flags]
internal enum AllAccess
{
    None = 0,
    Read = 1,
    Write = 2,
    All = -1
}

[FlecsStruct]
internal struct AllAccessHolder
{
    public AllAccess Flags;
}

[InlineArray(4)]
[FlecsStruct]
internal struct FloatValues
{
    private float _element0;
}

[FlecsStruct]
internal struct InlineArrayHolder
{
    public FloatValues Flat;
    public int Tail;
}

#pragma warning disable CS9184 // A pointer element type is not valid as a type argument.
[InlineArray(2)]
[FlecsStruct]
internal unsafe struct PointerValues
{
    private int* _element0;
}
#pragma warning restore CS9184

internal struct HookedMember :
    ICtorHook<HookedMember>,
    IDtorHook<HookedMember>,
    ICopyHook<HookedMember>,
    IMoveHook<HookedMember>
{
    public static int CtorCount;
    public static int DtorCount;
    public static int CopyCount;
    public static int MoveCount;

    public int Value;

    public static void Ctor(ref HookedMember data, TypeInfo _)
    {
        CtorCount++;
        data = default;
    }

    public static void Dtor(ref HookedMember data, TypeInfo _)
    {
        DtorCount++;
        data = default;
    }

    public static void Copy(ref HookedMember dst, ref HookedMember src, TypeInfo _)
    {
        CopyCount++;
        dst = src;
    }

    public static void Move(ref HookedMember dst, ref HookedMember src, TypeInfo _)
    {
        MoveCount++;
        dst = src;
        src = default;
    }
}

[FlecsStruct]
internal struct ReflectedOuter
{
    public HookedMember Member;
}

[FlecsStruct]
internal struct ReflectedNestedOuter
{
    public ReflectedOuter Outer;
}

internal struct PlainOuter
{
    public HookedMember Member;
}

internal struct ManualOuter
{
    public HookedMember Member;
}

internal struct ManualNestedOuter
{
    public ManualOuter Outer;
}

internal struct CompareOuter
{
    public HookedMember Member;
}

internal struct CallbackOuter
{
    public HookedMember Member;
}

internal struct ObserverOnlyOuter : IOnAddHook<ObserverOnlyOuter>
{
    public static int OnAddCount;
    public HookedMember Member;

    public static void OnAdd(Iter it, int i, ref ObserverOnlyOuter data)
    {
        OnAddCount++;
    }
}

internal struct TwoHookedOuter
{
    public HookedMember First;
    public HookedMember Second;
}

[InlineArray(4)]
[FlecsStruct]
internal struct HookedArray
{
    private HookedMember _element0;
}
