set script_path [ file dirname [ file normalize [ info script ] ] ]
open_hw
connect_hw_server -url localhost:3121
current_hw_target [get_hw_targets */xilinx_tcf/Digilent/210351A823F3A]
open_hw_target
refresh_hw_device -update_hw_probes false [lindex [get_hw_devices] 1]
set filename "${script_path}/../../Vivado/Zybo-Z7-20_TTC.sdk/design_1_wrapper_hw_platform_0/design_1_wrapper.bit"
set_property PROGRAM.FILE ${filename} [lindex [get_hw_devices] 1]
program_hw_devices [lindex [get_hw_devices] 1]
refresh_hw_device [lindex [get_hw_devices] 1]