do
    local p_srmp = Proto("srmp", "新生命远程消息交换协议")

    -- https://www.wireshark.org/docs/wsdg_html_chunked/lua_module_Proto.html#lua_class_ProtoField
    local FF_flag = {
        [7] = "[Reply]",
        [6] = "[Error/Oneway]"
    }

    -- local f_flag = ProtoField.uint8("SRMP.flag", "标记", base.HEX, FF_flag, 0xFF)
    local f_flag = ProtoField.uint8("SRMP.flag", "标记", base.HEX)
    local f_seq = ProtoField.uint8("SRMP.seq", "序列号", base.DEC)
    local f_length = ProtoField.uint16("SRMP.length", "长度", base.DEC)
    local f_action = ProtoField.string("SRMP.action", "动作", base.ASCII)
    local f_code = ProtoField.uint32("SRMP.code", "响应码", base.DEC)
    local f_data = ProtoField.string("SRMP.data", "内容", base.ASCII)

    p_srmp.fields = {f_flag, f_seq, f_length, f_action, f_code, f_data}

    local data_dis = Dissector.get("data")

    local function SRMP_dissector(buf, pkt, root)
        local buf_len = buf:len();
        if buf_len < 4 then
            return false
        end

        local tvb = buf:range()
        local v_flag = buf(0, 1)
        local v_seq = buf(1, 1)
        local v_length = buf(2, 2)
        local flag = tvb(0, 1):uint()

        local p = 4
        local len = tvb(p, 1):uint()
        local v_action = tvb(p + 1, len)

        p = p + 1 + len
        local v_code = 0
        -- if (flag & 0x80 == 0x80) then
        --    v_code = buf(p, 4)
        --    p = p + 4
        -- end

        len = tvb(p, 4):le_uint()
        local v_data = buf(p + 4, len)

        pkt.cols.protocol = "SRMP协议"

        local t = root:add(p_srmp, buf)
        t:add(f_flag, v_flag)
        t:add(f_seq, v_seq)
        t:add_le(f_length, v_length)

        local child, value = t:add_packet_field(f_action, v_action, ENC_UTF_8 + ENC_STRING)
        pkt.cols.info:append(' ' + v_action:string())

        -- if (flag & 0x80 == 0x80) then
        --    t:add_le(f_code, v_code)
        -- end

        t:add_packet_field(f_data, v_data, ENC_UTF_8 + ENC_STRING)

        return true
    end

    function p_srmp.dissector(buf, pkt, root)
        if SRMP_dissector(buf, pkt, root) then
            -- valid SRMP diagram
        else
            data_dis:call(buf, pkt, root)
        end
    end

    local udp_encap_table = DissectorTable.get("udp.port")
    udp_encap_table:add(5500, p_srmp)
    udp_encap_table:add(9999, p_srmp)
    udp_encap_table:add(3500, p_srmp)
end
