export namespace hid {
	
	export class DeviceInfo {
	    Index: number;
	    VendorID: number;
	    ProductID: number;
	    Product: string;
	    Serial: string;
	    Manufacturer: string;
	
	    static createFrom(source: any = {}) {
	        return new DeviceInfo(source);
	    }
	
	    constructor(source: any = {}) {
	        if ('string' === typeof source) source = JSON.parse(source);
	        this.Index = source["Index"];
	        this.VendorID = source["VendorID"];
	        this.ProductID = source["ProductID"];
	        this.Product = source["Product"];
	        this.Serial = source["Serial"];
	        this.Manufacturer = source["Manufacturer"];
	    }
	}
	export class GetKeymapReply {
	    id: string;
	    items: string[];
	
	    static createFrom(source: any = {}) {
	        return new GetKeymapReply(source);
	    }
	
	    constructor(source: any = {}) {
	        if ('string' === typeof source) source = JSON.parse(source);
	        this.id = source["id"];
	        this.items = source["items"];
	    }
	}
	export class GetSettingsReply {
	    id: string;
	    swap_fn_ctrl: boolean;
	    swap_alt_cmd: boolean;
	    bluetooth_disabled: boolean;
	    io_timing: number;
	
	    static createFrom(source: any = {}) {
	        return new GetSettingsReply(source);
	    }
	
	    constructor(source: any = {}) {
	        if ('string' === typeof source) source = JSON.parse(source);
	        this.id = source["id"];
	        this.swap_fn_ctrl = source["swap_fn_ctrl"];
	        this.swap_alt_cmd = source["swap_alt_cmd"];
	        this.bluetooth_disabled = source["bluetooth_disabled"];
	        this.io_timing = source["io_timing"];
	    }
	}
	export class SetKeymapReply {
	    id: string;
	    success: boolean;
	    error: string;
	
	    static createFrom(source: any = {}) {
	        return new SetKeymapReply(source);
	    }
	
	    constructor(source: any = {}) {
	        if ('string' === typeof source) source = JSON.parse(source);
	        this.id = source["id"];
	        this.success = source["success"];
	        this.error = source["error"];
	    }
	}
	export class SetSettingsRequest {
	    method_name: string;
	    id: string;
	    swap_fn_ctrl: boolean;
	    swap_alt_cmd: boolean;
	    bluetooth_disabled: boolean;
	    io_timing: number;
	
	    static createFrom(source: any = {}) {
	        return new SetSettingsRequest(source);
	    }
	
	    constructor(source: any = {}) {
	        if ('string' === typeof source) source = JSON.parse(source);
	        this.method_name = source["method_name"];
	        this.id = source["id"];
	        this.swap_fn_ctrl = source["swap_fn_ctrl"];
	        this.swap_alt_cmd = source["swap_alt_cmd"];
	        this.bluetooth_disabled = source["bluetooth_disabled"];
	        this.io_timing = source["io_timing"];
	    }
	}

}

export namespace main {
	
	export class SemVerInfo {
	    version: string;
	    gitCommit: string;
	    gitCommitCount: string;
	
	    static createFrom(source: any = {}) {
	        return new SemVerInfo(source);
	    }
	
	    constructor(source: any = {}) {
	        if ('string' === typeof source) source = JSON.parse(source);
	        this.version = source["version"];
	        this.gitCommit = source["gitCommit"];
	        this.gitCommitCount = source["gitCommitCount"];
	    }
	}

}

